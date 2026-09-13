using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

namespace OrizonAgents.Integration.Tests.Knowledge.Documents.Extraction;

public sealed class WordDocumentExtractorTests
{
    [Fact]
    public void CanExtract_AcceptsDocxExtension()
    {
        var extractor = new WordDocumentExtractor();

        bool result = extractor.CanExtract(
            "manual.docx",
            "application/octet-stream");

        Assert.True(result);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsParagraphs()
    {
        await using MemoryStream stream = CreateDocument(
            body =>
            {
                body.Append(
                    new Paragraph(
                        new Run(
                            new Text("Manual Comercial Orizon"))));

                body.Append(
                    new Paragraph(
                        new Run(
                            new Text("O protocolo de atendimento é ORZ-2026."))));
            });

        var extractor = new WordDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "manual.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            stream);

        Assert.Contains(
            "Documento: manual.docx",
            result.Text);

        Assert.Contains(
            "Manual Comercial Orizon",
            result.Text);

        Assert.Contains(
            "O protocolo de atendimento é ORZ-2026.",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_ConvertsTableToSemanticText()
    {
        await using MemoryStream stream = CreateDocument(
            body =>
            {
                var table = new Table();

                table.Append(
                    CreateRow(
                        "Produto",
                        "Preco",
                        "Estoque"));

                table.Append(
                    CreateRow(
                        "Notebook",
                        "3500",
                        "12"));

                table.Append(
                    CreateRow(
                        "Mouse",
                        "89",
                        "45"));

                body.Append(table);
            });

        var extractor = new WordDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "catalogo.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            stream);

        Assert.Contains("Tabela:", result.Text);

        Assert.Contains(
            "Colunas: Produto | Preco | Estoque",
            result.Text);

        Assert.Contains(
            "Produto: Notebook",
            result.Text);

        Assert.Contains(
            "Preco: 3500",
            result.Text);

        Assert.Contains(
            "Estoque: 12",
            result.Text);

        Assert.Contains(
            "Produto: Mouse",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PreservesParagraphsAndTables()
    {
        await using MemoryStream stream = CreateDocument(
            body =>
            {
                body.Append(
                    new Paragraph(
                        new Run(
                            new Text("Política Comercial"))));

                var table = new Table();

                table.Append(
                    CreateRow(
                        "Plano",
                        "Valor"));

                table.Append(
                    CreateRow(
                        "Premium",
                        "299"));

                body.Append(table);

                body.Append(
                    new Paragraph(
                        new Run(
                            new Text("Valores sujeitos às regras comerciais."))));
            });

        var extractor = new WordDocumentExtractor();

        var result = await extractor.ExtractAsync(
            "politica.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            stream);

        Assert.Contains(
            "Política Comercial",
            result.Text);

        Assert.Contains(
            "Plano: Premium",
            result.Text);

        Assert.Contains(
            "Valor: 299",
            result.Text);

        Assert.Contains(
            "Valores sujeitos às regras comerciais.",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_RejectsDocumentWithoutText()
    {
        await using MemoryStream stream = CreateDocument(
            _ =>
            {
            });

        var extractor = new WordDocumentExtractor();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => extractor.ExtractAsync(
                    "vazio.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    stream));

        Assert.Equal(
            "O documento Word não contém texto extraível.",
            exception.Message);
    }

    private static MemoryStream CreateDocument(
        Action<Body> configure)
    {
        var stream = new MemoryStream();

        using (WordprocessingDocument document =
               WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   true))
        {
            MainDocumentPart mainPart =
                document.AddMainDocumentPart();

            mainPart.Document =
                new Document();

            var body = new Body();

            configure(body);

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        stream.Position = 0;

        return stream;
    }

    private static TableRow CreateRow(
        params string[] values)
    {
        var row = new TableRow();

        foreach (string value in values)
        {
            row.Append(
                new TableCell(
                    new Paragraph(
                        new Run(
                            new Text(value)))));
        }

        return row;
    }
}
