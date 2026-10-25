namespace Shared.DTOs.Strategies;

public sealed record StrategyCashState(CurrencyCode Currency, decimal? FreeCash);

public sealed record StrategyPurchaseState(decimal RemainingQuantity, bool SaleApplied);

public sealed record StrategyHoldingState(decimal Quantity, bool TradeApplied);

public sealed record StrategyHoldingUpdate(decimal Quantity, PortfolioStockStatus Status);
