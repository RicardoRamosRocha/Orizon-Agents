using System.Text;
using OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

namespace OrizonAgents.Integration.Tests.Knowledge.Documents.Extraction;

public sealed class CsvDocumentExtractorTests
{
    [Fact]
    public void CanExtract_AcceptsCsvExtension()
    {
        var extractor = new CsvDocumentExtractor();

        bool result = extractor.CanExtract(
            "produtos.csv",
            "application/octet-stream");

        Assert.True(result);
    }

    [Fact]
    public async Task ExtractAsync_ConvertsSemicolonCsvToSemanticText()
    {
        const string csv =
            """
            Produto;Preco;Estoque
            Notebook;3500;12
            Mouse;89;45
            """;

        var extractor = new CsvDocumentExtractor();

        await using var stream = CreateStream(csv);

        var result = await extractor.ExtractAsync(
            "produtos.csv",
            "text/csv",
            stream);

        Assert.Contains("Tabela: produtos.csv", result.Text);
        Assert.Contains(
            "Colunas: Produto | Preco | Estoque",
            result.Text);
        Assert.Contains("Produto: Notebook", result.Text);
        Assert.Contains("Preco: 3500", result.Text);
        Assert.Contains("Estoque: 12", result.Text);
        Assert.Contains("Produto: Mouse", result.Text);
        Assert.Contains("Preco: 89", result.Text);
        Assert.Contains("Estoque: 45", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SupportsCommaSeparatedCsv()
    {
        const string csv =
            """
            Nome,Cidade,Estado
            Ricardo,Belo Horizonte,MG
            Maria,Contagem,MG
            """;

        var extractor = new CsvDocumentExtractor();

        await using var stream = CreateStream(csv);

        var result = await extractor.ExtractAsync(
            "clientes.csv",
            "text/csv",
            stream);

        Assert.Contains("Nome: Ricardo", result.Text);
        Assert.Contains(
            "Cidade: Belo Horizonte",
            result.Text);
        Assert.Contains("Estado: MG", result.Text);
        Assert.Contains("Nome: Maria", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PreservesDelimiterInsideQuotedField()
    {
        const string csv =
            """
            Produto,Descricao,Preco
            Notebook,"Notebook, 16 GB RAM",3500
            """;

        var extractor = new CsvDocumentExtractor();

        await using var stream = CreateStream(csv);

        var result = await extractor.ExtractAsync(
            "produtos.csv",
            "text/csv",
            stream);

        Assert.Contains("Produto: Notebook", result.Text);
        Assert.Contains(
            "Descricao: Notebook, 16 GB RAM",
            result.Text);
        Assert.Contains("Preco: 3500", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SupportsEscapedQuotes()
    {
        const string csv =
            "Produto,Descricao\n" +
            "Notebook,\"Modelo \"\"Premium\"\"\"\n";

        var extractor = new CsvDocumentExtractor();

        await using var stream = CreateStream(csv);

        var result = await extractor.ExtractAsync(
            "produtos.csv",
            "text/csv",
            stream);

        Assert.Contains(
            "Descricao: Modelo \"Premium\"",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_RejectsEmptyContent()
    {
        var extractor = new CsvDocumentExtractor();

        await using var stream = CreateStream(
            "   ");

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => extractor.ExtractAsync(
                    "vazio.csv",
                    "text/csv",
                    stream));

        Assert.Equal(
            "A planilha CSV não contém dados.",
            exception.Message);
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }
}
