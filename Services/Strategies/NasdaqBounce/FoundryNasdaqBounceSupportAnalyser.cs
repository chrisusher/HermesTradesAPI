using Azure.AI.OpenAI;
using Azure.Identity;
using ChrisUsher.Core.Shared.Converters;
using OpenAI.Chat;
using Shared.Config;

namespace Services.Strategies.NasdaqBounce;

public sealed class FoundryNasdaqBounceSupportAnalyser : INasdaqBounceSupportAnalyser
{
    private readonly ChatClient _client;
    private readonly NasdaqBounceOptions _options;
    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "action": {
              "type": "string",
              "enum": ["Hold", "Sell", "InsufficientData"]
            },
            "explanation": {
              "type": "string"
            },
            "evidence": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "time": {
                    "type": "string"
                  },
                  "price": {
                    "type": "number"
                  }
                },
                "required": ["time", "price"],
                "additionalProperties": false
              }
            }
          },
          "required": ["action", "explanation", "evidence"],
          "additionalProperties": false
        }
        """;

    public FoundryNasdaqBounceSupportAnalyser(NasdaqBounceOptions options)
    {
        _options = options;
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions {
            ManagedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
        });
        _client = new AzureOpenAIClient(new Uri(options.FoundryEndpoint), credential).GetChatClient(options.FoundryDeployment);
    }

    public async Task<SupportDecision> AnalyseAsync(NasdaqBounceMarketSnapshot snapshot, NasdaqBounceSession session,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.FoundryTimeoutSeconds));

        var input = JsonSerializer.Serialize(new
        {
            symbol = "QQQ",
            sessionDate = session.Date,
            asOf = now,
            currentPrice = snapshot.Price,
            priceTime = snapshot.PriceTime,
            candles = snapshot.Candles.Select(x => new
            {
                time = x.Date,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume
            })
        });

        var completion = await _client.CompleteChatAsync(
            [new SystemChatMessage("""
                You analyse whether QQQ has broken an intraday support line for the current regular trading session.
                Use only the supplied OHLCV candles and as-of price. Do not use external sources or invent evidence.
                Identify a credible support line using observed intraday lows, then assess whether price action confirms a break.
                Sell only for a supported break; otherwise Hold. If there is too little evidence, return InsufficientData.

                For Sell, provide at least two observed candle low points defining the support line, ordered by time.
                Explain the support line and break using specific supplied prices and times. A forming candle is provisional.
                """), new UserChatMessage(input)],

            new ChatCompletionOptions {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "nasdaq_bounce_support",
                    BinaryData.FromString(Schema),
                    jsonSchemaIsStrict: true)
            },
            timeout.Token);
        var result = completion.Value;

        if (result.FinishReason != ChatFinishReason.Stop || result.Content.Count == 0)
        {
            throw new InvalidOperationException("Foundry did not return a complete support decision.");
        }

        return ParseAndValidate(string.Concat(result.Content.Select(x => x.Text)), snapshot, session, now);
    }

    public static SupportDecision ParseAndValidate(
        string json,
        NasdaqBounceMarketSnapshot snapshot,
        NasdaqBounceSession session,
        DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var action = root.GetProperty("action").GetString() switch {
            "Hold" => SupportAction.Hold,
            "Sell" => SupportAction.Sell,
            "InsufficientData" => SupportAction.InsufficientData,
            _ => throw new InvalidOperationException("Invalid Foundry action.")
        };
        var explanation = root.GetProperty("explanation").GetString();

        if (string.IsNullOrWhiteSpace(explanation) || explanation.Length > 4000)
        {
            throw new InvalidOperationException("Invalid Foundry explanation.");
        }

        var evidence = root.GetProperty("evidence").EnumerateArray().Select(x =>
            new SupportPoint(x.GetProperty("time").GetDateTimeOffset(), x.GetProperty("price").GetDecimal())).ToList();

        if (evidence.Count > 50 || evidence.Any(x => x.Time < session.Open || x.Time > now || x.Price <= 0 ||
            !snapshot.Candles.Any(c => new DateTimeOffset(DateTimeHandler.ConvertToUtc(c.Date)) == x.Time && c.Low == x.Price)))
        {
            throw new InvalidOperationException("Foundry evidence does not match supplied intraday lows.");
        }

        if (action == SupportAction.Sell)
        {
            if (evidence.Count < 2 || evidence.Zip(evidence.Skip(1)).Any(x => x.First.Time >= x.Second.Time))
            {
                throw new InvalidOperationException("Sell requires ordered support-line anchors.");
            }

            var first = evidence[0];
            var last = evidence[^1];

            var supportNow = first.Price + (last.Price - first.Price) *
                (decimal)((snapshot.PriceTime - first.Time).TotalSeconds / (last.Time - first.Time).TotalSeconds);

            if (snapshot.Price >= supportNow)
            {
                throw new InvalidOperationException("Price has not broken the identified support line.");
            }
        }

        return new SupportDecision(action, explanation, evidence);
    }
}
