namespace Shared.DTOs.Stocks;

public class PortfolioStock : StockSummary
{
    public StockSummary ToSummary()
    {
        return new StockSummary
        {
            Symbol = Symbol,
            CompanyName = CompanyName,
            ExchangeName = ExchangeName,
            StockType = StockType,
            Country = Country,
            Status = Status,
            Created = Created,
            Updated = Updated,
            PreviousClosePrice = PreviousClosePrice,
            CurrencyCode = CurrencyCode,
        };
    }
}
