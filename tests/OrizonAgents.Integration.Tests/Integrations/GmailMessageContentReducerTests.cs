using OrizonAgents.Infrastructure.Integrations.Gmail;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class GmailMessageContentReducerTests
{
    private readonly GmailMessageContentReducer _sut = new();

    [Fact]
    public void Reduce_SimpleMessage_PreservesSemanticContent()
    {
        string result = _sut.Reduce(
            "Olá, equipe!\nO contrato foi aprovado.",
            isHtml: false)!;

        Assert.Equal(
            "Olá, equipe!\nO contrato foi aprovado.",
            result);
    }

    [Fact]
    public void Reduce_ExcessWhitespace_NormalizesLinesAndSpaces()
    {
        string result = _sut.Reduce(
            "  Olá,   equipe!\r\n\r\n\r\n  Próximo passo.  ",
            isHtml: false)!;

        Assert.Equal("Olá, equipe!\n\nPróximo passo.", result);
    }

    [Fact]
    public void Reduce_EvidentReply_RemovesPreviousMessage()
    {
        string result = _sut.Reduce(
            "Resposta atual.\n\nEm sex., 4 de set. de 2026, Cliente escreveu:\n> Texto anterior\n> Mais texto anterior",
            isHtml: false)!;

        Assert.Equal("Resposta atual.", result);
    }

    [Fact]
    public void Reduce_EvidentShortSignature_RemovesIt()
    {
        string result = _sut.Reduce(
            "Mensagem principal.\n\n-- \nRicardo\nOrizon",
            isHtml: false)!;

        Assert.Equal("Mensagem principal.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reduce_EmptyContent_ReturnsNull(string? content)
    {
        Assert.Null(_sut.Reduce(content, isHtml: false));
    }

    [Fact]
    public void Reduce_Html_ProducesReadableTextWithoutScripts()
    {
        string result = _sut.Reduce(
            "<style>.x{color:red}</style><p>Olá &amp; bem-vindo</p><p>Segundo bloco<br>nova linha</p><script>alert('x')</script>",
            isHtml: true)!;

        Assert.Equal(
            "Olá & bem-vindo\nSegundo bloco\nnova linha",
            result);
    }

    [Fact]
    public void Reduce_LargeMessage_RetainsBeginningAndEndWithinGmailLimit()
    {
        string content =
            "INÍCIO IMPORTANTE\n\n" +
            string.Join("\n", Enumerable.Repeat(new string('x', 100), 100)) +
            "\n\nFIM IMPORTANTE";

        string result = _sut.Reduce(content, isHtml: false)!;

        Assert.True(result.Length <= GmailMessageContentReducer.MaximumBodyCharacters);
        Assert.StartsWith("INÍCIO IMPORTANTE", result);
        Assert.EndsWith("FIM IMPORTANTE", result);
        Assert.Contains("Trecho intermediário reduzido", result);
    }

    [Fact]
    public void Reduce_DoesNotTreatIsolatedQuoteOrDashesAsReplyOrSignature()
    {
        string content = "Comparação:\n> 10 unidades\n-- condição comercial";

        Assert.Equal(content, _sut.Reduce(content, isHtml: false));
    }
}
