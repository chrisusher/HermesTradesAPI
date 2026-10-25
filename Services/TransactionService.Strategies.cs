namespace Services;

public partial class TransactionService
{
    public Task<decimal> GetRemainingStrategyPurchaseQuantityAsync(
        Guid transactionId,
        Guid portfolioId,
        CancellationToken cancellationToken) =>
        _transactionRepository.GetRemainingPurchaseQuantityAsync(transactionId, portfolioId, cancellationToken);

    public async Task ApplyStrategyTradeAsync(StrategyTrade trade, CancellationToken cancellationToken = default)
    {
        if (trade.TransactionId == Guid.Empty ||
            trade.PortfolioId == Guid.Empty ||
            trade.UserId == Guid.Empty ||
            trade.StockId == Guid.Empty ||
            string.IsNullOrWhiteSpace(trade.StrategyId) ||
            string.IsNullOrWhiteSpace(trade.StrategyVersion) ||
            string.IsNullOrWhiteSpace(trade.Symbol) ||
            trade.Price <= 0 ||
            trade.Quantity == 0 ||
            trade.SourceCandleTime > trade.Time ||
            Math.Sign(trade.Quantity) != Math.Sign(trade.PortfolioAmount) ||
            Math.Sign(trade.Quantity) != Math.Sign(trade.UserAmount))
        {
            throw new InvalidOperationException("Invalid strategy trade intent.");
        }

        // Validate a sale before its proceeds can be credited to the portfolio.
        if (trade.Quantity < 0)
        {
            if (trade.OriginalTransactionId is null)
            {
                throw new InvalidOperationException("A strategy sale must reference its purchase.");
            }

            var purchase = await _transactionRepository.GetStrategyPurchaseAsync(trade, cancellationToken);

            if (!purchase.SaleApplied && purchase.RemainingQuantity < -trade.Quantity)
            {
                throw new InvalidOperationException("Sale exceeds the remaining original purchase.");
            }

            var holding = await _portfolioRepository.GetStrategyHoldingStateAsync(trade, cancellationToken)
                ?? throw new InvalidOperationException("Strategy holding is missing.");

            if (!holding.TradeApplied && holding.Quantity < -trade.Quantity)
            {
                throw new InvalidOperationException("Sale exceeds the remaining strategy holding.");
            }
        }

        // Keep separate document commits in this order so a persisted intent can resume after any step.
        await _portfolioRepository.UpdateStrategyCashAsync(
            trade,
            portfolio => CalculateCash(trade, portfolio),
            cancellationToken);

        await _transactionRepository.RecordStrategyTransactionAsync(trade, cancellationToken);
        await _portfolioRepository.UpdateStrategyHoldingAsync(
            trade,
            holding => CalculateHolding(trade, holding),
            cancellationToken);

        if (trade.OriginalTransactionId is not null)
        {
            await _transactionRepository.UpdateStrategyPurchaseAsync(
                trade,
                purchase => CalculateRemainingQuantity(trade, purchase),
                cancellationToken);
        }
    }

    private static decimal CalculateCash(StrategyTrade trade, StrategyCashState portfolio)
    {
        if (portfolio.Currency != trade.PortfolioCurrency ||
            portfolio.FreeCash is null ||
            portfolio.FreeCash - trade.PortfolioAmount < 0)
        {
            throw new InvalidOperationException("Insufficient cash or mismatched portfolio currency.");
        }

        return portfolio.FreeCash.Value - trade.PortfolioAmount;
    }

    private static StrategyHoldingUpdate CalculateHolding(StrategyTrade trade, StrategyHoldingState? holding)
    {
        if (holding is null && trade.Quantity < 0)
        {
            throw new InvalidOperationException("Strategy holding is missing.");
        }

        var quantity = (holding?.Quantity ?? 0) + trade.Quantity;

        if (quantity < 0)
        {
            throw new InvalidOperationException("Concurrent holding change prevents the strategy sale; reconciliation is required.");
        }

        return new StrategyHoldingUpdate(
            quantity,
            quantity > 0 ? PortfolioStockStatus.Active : PortfolioStockStatus.FullySold);
    }

    private static decimal CalculateRemainingQuantity(StrategyTrade trade, StrategyPurchaseState purchase)
    {
        if (purchase.RemainingQuantity < -trade.Quantity)
        {
            throw new InvalidOperationException("Sale exceeds its original purchase.");
        }

        return purchase.RemainingQuantity + trade.Quantity;
    }
}
