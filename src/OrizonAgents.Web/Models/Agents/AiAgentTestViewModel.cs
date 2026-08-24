using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Agents;

public sealed class AiAgentTestViewModel
{
    public Guid AgentId { get; set; }

    public Guid? ConversationId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string? AgentDescription { get; set; }

    [Required(ErrorMessage = "Digite uma mensagem.")]
    [Display(Name = "Mensagem")]
    public string Message { get; set; } = string.Empty;

    public List<AiAgentTestMessageViewModel> Messages { get; set; } = new();

    public string? ErrorMessage { get; set; }
}

public sealed class AiAgentTestMessageViewModel
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

