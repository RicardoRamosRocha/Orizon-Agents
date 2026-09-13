using ClosedXML.Excel;
using OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

namespace OrizonAgents.Integration.Tests.Knowledge.Documents.Extraction;

public sealed class ExcelDocumentExtractorTests
{
    [Fact]
    public void CanExtract_AcceptsXlsxExtension()
    {
        var extractor = new ExcelDocumentExtractor();

        bool result = extractor.CanExtract(
            "produtos.xlsx",
            "application/octet-stream");

        Assert.True(result);
    }

    [Fact]
    public async Task ExtractAsync_ConvertsWorksheetToSemanticText()
    {
        await using MemoryStream stream = CreateWorkbook(
            workbook =>
            {
                IXLWorksheet sheet =
                    workbook.Worksheets.Add("Produtos");

                sheet.Cell(1, 1).Value = "Produto";
                sheet.Cell(1, 2).Value = "Preco";
                sheet.Cell(1, 3).Value = "Estoque";

                sheet.Cell(2, 1).Value = "Notebook";
                sheet.Cell(2, 2).Value = 3500;
                sheet.Cell(2, 3).Value = 12;

                sheet.Cell(3, 1).Value = "Mouse";
                sheet.Cell(3, 2).Value = 89;
                sheet.Cell(3, 3).Value = 45;
            });

        var extractor = new ExcelDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "produtos.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream);

        Assert.Contains("Planilha: Produtos", result.Text);
        Assert.Contains("Arquivo: produtos.xlsx", result.Text);
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
    public async Task ExtractAsync_PreservesMultipleWorksheets()
    {
        await using MemoryStream stream = CreateWorkbook(
            workbook =>
            {
                IXLWorksheet products =
                    workbook.Worksheets.Add("Produtos");

                products.Cell(1, 1).Value = "Produto";
                products.Cell(1, 2).Value = "Preco";
                products.Cell(2, 1).Value = "Notebook";
                products.Cell(2, 2).Value = 3500;

                IXLWorksheet customers =
                    workbook.Worksheets.Add("Clientes");

                customers.Cell(1, 1).Value = "Nome";
                customers.Cell(1, 2).Value = "Cidade";
                customers.Cell(2, 1).Value = "Maria";
                customers.Cell(2, 2).Value = "Contagem";
            });

        var extractor = new ExcelDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "dados.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream);

        Assert.Contains("Planilha: Produtos", result.Text);
        Assert.Contains("Produto: Notebook", result.Text);
        Assert.Contains("Preco: 3500", result.Text);

        Assert.Contains("Planilha: Clientes", result.Text);
        Assert.Contains("Nome: Maria", result.Text);
        Assert.Contains("Cidade: Contagem", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SkipsEmptyCells()
    {
        await using MemoryStream stream = CreateWorkbook(
            workbook =>
            {
                IXLWorksheet sheet =
                    workbook.Worksheets.Add("Clientes");

                sheet.Cell(1, 1).Value = "Nome";
                sheet.Cell(1, 2).Value = "Telefone";
                sheet.Cell(1, 3).Value = "Cidade";

                sheet.Cell(2, 1).Value = "Maria";
                sheet.Cell(2, 3).Value = "Belo Horizonte";
            });

        var extractor = new ExcelDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "clientes.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream);

        Assert.Contains("Nome: Maria", result.Text);
        Assert.Contains(
            "Cidade: Belo Horizonte",
            result.Text);

        Assert.DoesNotContain(
            "Telefone:",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_RejectsWorkbookWithoutData()
    {
        await using MemoryStream stream = CreateWorkbook(
            workbook =>
            {
                workbook.Worksheets.Add("Vazia");
            });

        var extractor = new ExcelDocumentExtractor();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => extractor.ExtractAsync(
                    "vazio.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    stream));

        Assert.Equal(
            "A planilha Excel não contém dados.",
            exception.Message);
    }

    private static MemoryStream CreateWorkbook(
        Action<XLWorkbook> configure)
    {
        using var workbook = new XLWorkbook();

        configure(workbook);

        var stream = new MemoryStream();

        workbook.SaveAs(stream);

        stream.Position = 0;

        return stream;
    }
}
