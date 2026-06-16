namespace Shared.Config;

public class FunctionsConfig
{    
    public bool IsDebug { get; set; }

    public bool SaveToBlobStorage { get; set; }

    public bool SendToServiceBus { get; set; }

    public bool UsePriceServiceForStats { get; set; }
}
