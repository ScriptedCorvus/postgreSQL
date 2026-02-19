using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class SqlParameterDetectorTests
{
    [Test]
    public void DetectParameters_NullOrEmpty_ReturnsEmpty()
    {
        SqlParameterDetector.DetectParameters(null!).Should().BeEmpty();
        SqlParameterDetector.DetectParameters("").Should().BeEmpty();
        SqlParameterDetector.DetectParameters("   ").Should().BeEmpty();
    }

    [Test]
    public void DetectParameters_AtParam_DetectsCorrectly()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT * FROM users WHERE id = @userId");
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("userId");
        result[0].Prefix.Should().Be("@");
    }

    [Test]
    public void DetectParameters_ColonParam_DetectsCorrectly()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT * FROM users WHERE id = :userId");
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("userId");
        result[0].Prefix.Should().Be(":");
    }

    [Test]
    public void DetectParameters_MultipleParams_ReturnsAll()
    {
        var sql = "SELECT * FROM users WHERE name = @name AND age > @age AND email = @email";
        var result = SqlParameterDetector.DetectParameters(sql);
        result.Should().HaveCount(3);
        result.Select(p => p.Name).Should().Contain("name").And.Contain("age").And.Contain("email");
    }

    [Test]
    public void DetectParameters_DuplicateParams_ReturnsDistinct()
    {
        var sql = "SELECT * FROM users WHERE id = @id OR parent_id = @id";
        var result = SqlParameterDetector.DetectParameters(sql);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("id");
    }

    [Test]
    public void DetectParameters_PostgreSqlCast_IgnoresDoublColon()
    {
        var sql = "SELECT value::int FROM t WHERE id = @id";
        var result = SqlParameterDetector.DetectParameters(sql);
        // Should detect @id but NOT "int" from ::int
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("id");
    }

    [Test]
    public void DetectParameters_InsideSingleQuotedString_IsIgnored()
    {
        var sql = "SELECT * FROM t WHERE col = '@notaparam'";
        var result = SqlParameterDetector.DetectParameters(sql);
        result.Should().BeEmpty();
    }

    [Test]
    public void DetectParameters_InsideLineComment_IsIgnored()
    {
        var sql = "SELECT * FROM t -- WHERE id = @ignored\nWHERE id = @actual";
        var result = SqlParameterDetector.DetectParameters(sql);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("actual");
    }

    [Test]
    public void DetectParameters_InsideBlockComment_IsIgnored()
    {
        var sql = "SELECT * FROM t /* WHERE id = @ignored */ WHERE id = @actual";
        var result = SqlParameterDetector.DetectParameters(sql);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("actual");
    }

    // -- HasParameters --

    [Test]
    public void HasParameters_WithParams_ReturnsTrue()
    {
        SqlParameterDetector.HasParameters("SELECT @id").Should().BeTrue();
    }

    [Test]
    public void HasParameters_WithoutParams_ReturnsFalse()
    {
        SqlParameterDetector.HasParameters("SELECT 1").Should().BeFalse();
    }

    [Test]
    public void HasParameters_NullOrEmpty_ReturnsFalse()
    {
        SqlParameterDetector.HasParameters(null!).Should().BeFalse();
        SqlParameterDetector.HasParameters("").Should().BeFalse();
    }

    // -- Type Inference --

    [Test]
    public void DetectParameters_InfersIntegerForIdParam()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT @userId");
        result.Should().HaveCount(1);
        result[0].InferredType.Should().Be(SqlParameterType.Integer);
    }

    [Test]
    public void DetectParameters_InfersDecimalForPriceParam()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT @totalPrice");
        result.Should().HaveCount(1);
        result[0].InferredType.Should().Be(SqlParameterType.Decimal);
    }

    [Test]
    public void DetectParameters_InfersDateTimeForDateParam()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT @createdDate");
        result.Should().HaveCount(1);
        result[0].InferredType.Should().Be(SqlParameterType.DateTime);
    }

    [Test]
    public void DetectParameters_InfersBooleanForIsActiveParam()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT @is_active");
        result.Should().HaveCount(1);
        result[0].InferredType.Should().Be(SqlParameterType.Boolean);
    }

    [Test]
    public void DetectParameters_InfersStringForGenericParam()
    {
        var result = SqlParameterDetector.DetectParameters("SELECT @username");
        result.Should().HaveCount(1);
        result[0].InferredType.Should().Be(SqlParameterType.String);
    }
}
