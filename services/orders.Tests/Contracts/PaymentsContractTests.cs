using System.Text.Json;
using CogniDev.Testing;
using Xunit;
using PaymentContract = Orders.Contracts.Payment;

namespace Orders.Tests.Contracts;

/// orders → payments. Unlike the other two boundaries this read model is a real,
/// public type in the orders service (`Orders.Contracts.Payment` — a COPY of the
/// payment shape, not payments' entity), so SchemaGuard asserts against it directly.
/// The compiler cannot check that copy against the provider, which is exactly why
/// these fixtures exist.
public sealed class PaymentsContractTests
{
    /// The payment lifecycle orders is prepared to see. `Contracts.Payment.Status` is
    /// still the ported STRING column, while payments now serves its status as the
    /// `PaymentStatus` enum (serialized as its ordinal) — so a conforming response may
    /// carry either spelling. Both are accepted here; neither an unknown name nor an
    /// out-of-range ordinal is.
    private static readonly string[] KnownStatuses = ["pending", "charged", "refunded", "failed"];

    public static IEnumerable<object[]> Fixtures() => ContractBoundaries.Fixtures("payments");

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_satisfies_the_read_model(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);

        SchemaGuard.AssertResponseSatisfies(json, typeof(PaymentContract));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_holds_the_value_invariants(string fixturePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = doc.RootElement;

        // The saga records this id to drive its refund compensation, so it must be a
        // real identity — a zero here would make the compensation unaddressable.
        Assert.True(root.GetProperty("id").GetInt32() > 0, "a payment id must be a positive identity");

        Assert.True(root.GetProperty("orderId").GetInt32() > 0, "a payment must reference a real order");

        Assert.True(root.GetProperty("amount").GetDecimal() >= 0m, "money must never be negative");

        var status = root.GetProperty("status");
        if (status.ValueKind == JsonValueKind.String)
        {
            Assert.Contains(status.GetString()!.ToLowerInvariant(), KnownStatuses);
        }
        else
        {
            Assert.Equal(JsonValueKind.Number, status.ValueKind);
            var ordinal = status.GetInt32();
            Assert.InRange(ordinal, 0, KnownStatuses.Length - 1);
        }
    }

    /// The one thing the saga branches on: a completed charge. If payments ever stops
    /// reporting a charged payment as charged, the saga would place an unpaid order.
    [Fact]
    public void A_recorded_charge_reports_as_charged()
    {
        var path = ContractBoundaries.Fixtures("payments")
            .Select(row => (string)row[0])
            .Single(p => Path.GetFileNameWithoutExtension(p) == "charge-payment");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var status = doc.RootElement.GetProperty("status");

        var name = status.ValueKind == JsonValueKind.String
            ? status.GetString()!.ToLowerInvariant()
            : KnownStatuses[status.GetInt32()];

        Assert.Equal("charged", name);
    }
}
