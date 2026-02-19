using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class SqlStatementSplitterTests
{
    [Test]
    public void Split_NullOrEmpty_ReturnsEmpty()
    {
        SqlStatementSplitter.Split(null!).Should().BeEmpty();
        SqlStatementSplitter.Split("").Should().BeEmpty();
        SqlStatementSplitter.Split("   ").Should().BeEmpty();
    }

    [Test]
    public void Split_SingleStatement_ReturnsSingle()
    {
        var result = SqlStatementSplitter.Split("SELECT 1");
        result.Should().HaveCount(1);
        result[0].Should().Be("SELECT 1");
    }

    [Test]
    public void Split_SingleStatementWithSemicolon_ReturnsSingle()
    {
        var result = SqlStatementSplitter.Split("SELECT 1;");
        result.Should().HaveCount(1);
        result[0].Should().Be("SELECT 1");
    }

    [Test]
    public void Split_MultipleStatements_SplitsCorrectly()
    {
        var result = SqlStatementSplitter.Split("SELECT 1; SELECT 2; SELECT 3");
        result.Should().HaveCount(3);
        result[0].Should().Be("SELECT 1");
        result[1].Should().Be("SELECT 2");
        result[2].Should().Be("SELECT 3");
    }

    [Test]
    public void Split_SemicolonInsideSingleQuotedString_DoesNotSplit()
    {
        var result = SqlStatementSplitter.Split("SELECT 'hello;world'");
        result.Should().HaveCount(1);
        result[0].Should().Contain(";");
    }

    [Test]
    public void Split_SemicolonInsideDoubleQuotedIdentifier_DoesNotSplit()
    {
        var result = SqlStatementSplitter.Split("SELECT \"col;name\" FROM t");
        result.Should().HaveCount(1);
        result[0].Should().Contain(";");
    }

    [Test]
    public void Split_EscapedQuoteInsideString_HandlesProperly()
    {
        // Two single quotes inside a string: 'it''s'
        var result = SqlStatementSplitter.Split("SELECT 'it''s a test'; SELECT 2");
        result.Should().HaveCount(2);
        result[0].Should().Contain("it''s");
    }

    [Test]
    public void Split_LineComment_DoesNotSplitAtSemicolonInComment()
    {
        var sql = "SELECT 1 -- this is a comment; not a split\n; SELECT 2";
        var result = SqlStatementSplitter.Split(sql);
        result.Should().HaveCount(2);
    }

    [Test]
    public void Split_BlockComment_DoesNotSplitAtSemicolonInComment()
    {
        var sql = "SELECT 1 /* comment; with semicolons */; SELECT 2";
        var result = SqlStatementSplitter.Split(sql);
        result.Should().HaveCount(2);
    }

    [Test]
    public void Split_PostgreSqlDollarQuoting_DoesNotSplitInside()
    {
        var sql = @"CREATE FUNCTION test() RETURNS void AS $$
BEGIN
    RAISE NOTICE 'hello;world';
END;
$$ LANGUAGE plpgsql; SELECT 1";
        var result = SqlStatementSplitter.Split(sql);
        result.Should().HaveCount(2);
        result[0].Should().Contain("$$");
    }

    [Test]
    public void Split_CustomDollarTag_DoesNotSplitInside()
    {
        var sql = @"CREATE FUNCTION test() RETURNS void AS $body$
BEGIN
    RAISE NOTICE 'hello';
END;
$body$ LANGUAGE plpgsql; SELECT 1";
        var result = SqlStatementSplitter.Split(sql);
        result.Should().HaveCount(2);
        result[0].Should().Contain("$body$");
    }

    [Test]
    public void Split_MySqlDelimiterDirective_ChangesDelimiter()
    {
        var sql = @"DELIMITER //
CREATE PROCEDURE test()
BEGIN
    SELECT 1;
END //
DELIMITER ;
SELECT 2;";
        var result = SqlStatementSplitter.Split(sql);
        // Should have: CREATE PROCEDURE block, SELECT 2
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(s => s.Contains("CREATE PROCEDURE"));
    }

    [Test]
    public void Split_EmptyStatements_AreSkipped()
    {
        var result = SqlStatementSplitter.Split(";;; SELECT 1 ;;;");
        result.Should().HaveCount(1);
        result[0].Should().Be("SELECT 1");
    }

    [Test]
    public void Split_MultiLineStatements_PreservesContent()
    {
        var sql = "SELECT\n  col1,\n  col2\nFROM\n  table1;\nSELECT 2";
        var result = SqlStatementSplitter.Split(sql);
        result.Should().HaveCount(2);
        result[0].Should().Contain("col1");
        result[0].Should().Contain("col2");
    }

    // -- GetStatementAtCursor --

    [Test]
    public void GetStatementAtCursor_ReturnsCorrectStatement()
    {
        var sql = "SELECT 1; SELECT 2; SELECT 3";
        // Cursor in the middle of "SELECT 2" (offset ~12)
        var result = SqlStatementSplitter.GetStatementAtCursor(sql, 12);
        result.Should().Be("SELECT 2");
    }

    [Test]
    public void GetStatementAtCursor_CursorAtStart_ReturnsFirst()
    {
        var sql = "SELECT 1; SELECT 2";
        var result = SqlStatementSplitter.GetStatementAtCursor(sql, 0);
        result.Should().Be("SELECT 1");
    }

    [Test]
    public void GetStatementAtCursor_EmptyInput_ReturnsNull()
    {
        SqlStatementSplitter.GetStatementAtCursor("", 0).Should().BeNull();
    }
}
