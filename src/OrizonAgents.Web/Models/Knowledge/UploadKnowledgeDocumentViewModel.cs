using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OrizonAgents.Web.Models.Knowledge;

public sealed class UploadKnowledgeDocumentViewModel
{
    [Required]
    public Guid KnowledgeBaseId { get; set; }

    [Required(ErrorMessage = "Selecione um arquivo.")]
    [Display(Name = "Documento")]
    public IFormFile? File { get; set; }
}
