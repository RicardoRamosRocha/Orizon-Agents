using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Web.Models.Agents;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("agentes")]
public sealed class AgentsController : Controller
{
    private readonly IAiAgentService _aiAgentService;
    private readonly IAiAgentRunner _aiAgentRunner;
    private readonly IAiConversationService _aiConversationService;

    public AgentsController(
        IAiAgentService aiAgentService,
        IAiAgentRunner aiAgentRunner,
        IAiConversationService aiConversationService)
    {
        _aiAgentService = aiAgentService;
        _aiAgentRunner = aiAgentRunner;
        _aiConversationService = aiConversationService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<AiAgentListItemDto> agents =
            await _aiAgentService.ListAsync(cancellationToken);

        return View(agents);
    }

    [HttpGet("novo")]
    public IActionResult Create()
    {
        return View(new AiAgentFormViewModel());
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AiAgentFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        OperationResult<Guid> result =
            await _aiAgentService.CreateAsync(
                new CreateAiAgentRequest(
                    GetTenantId(),
                    form.Name,
                    form.Description,
                    form.SystemPrompt,
                    form.Provider,
                    form.Model,
                    form.Temperature),
                cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(form);
        }

        TempData["StatusMessage"] = "Agente criado. Agora faça um teste para ver como ele se comporta.";
        return RedirectToAction(
            nameof(Test),
            new { id = result.Value });
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken)
    {
        AiAgentDetailsDto? agent =
            await _aiAgentService.GetAsync(id, cancellationToken);

        if (agent is null)
        {
            return NotFound();
        }

        return View(new AiAgentFormViewModel
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            SystemPrompt = agent.SystemPrompt,
            Provider = agent.Provider,
            Model = agent.Model,
            Temperature = agent.Temperature,
            IsActive = agent.IsActive
        });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        AiAgentFormViewModel form,
        CancellationToken cancellationToken)
    {
        form.Id = id;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        OperationResult result =
            await _aiAgentService.UpdateAsync(
                new UpdateAiAgentRequest(
                    id,
                    form.Name,
                    form.Description,
                    form.SystemPrompt,
                    form.Provider,
                    form.Model,
                    form.Temperature),
                cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(form);
        }

        TempData["StatusMessage"] = "Agente atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        bool activate,
        CancellationToken cancellationToken)
    {
        OperationResult result = activate
            ? await _aiAgentService.ActivateAsync(id, cancellationToken)
            : await _aiAgentService.DeactivateAsync(id, cancellationToken);

        TempData["StatusMessage"] = result.Succeeded
            ? activate
                ? "Agente ativado."
                : "Agente desativado."
            : string.Join(" ", result.Errors);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/testar")]
    public async Task<IActionResult> Test(
        Guid id,
        Guid? conversationId,
        CancellationToken cancellationToken)
    {
        AiAgentDetailsDto? agent =
            await _aiAgentService.GetAsync(id, cancellationToken);

        if (agent is null)
        {
            return NotFound();
        }

        IReadOnlyList<AiConversationListItemDto> conversations =
            await _aiConversationService.ListAsync(
                id,
                cancellationToken);

        var viewModel = new AiAgentTestViewModel
        {
            AgentId = agent.Id,
            AgentName = agent.Name,
            AgentDescription = agent.Description,
            ConversationId = conversationId,
            Conversations = conversations
                .Select(conversation =>
                    new AiAgentTestConversationViewModel
                    {
                        Id = conversation.Id,
                        Title = string.IsNullOrWhiteSpace(conversation.Title)
                            ? "Nova conversa"
                            : conversation.Title,
                        CreatedAtUtc = conversation.CreatedAtUtc,
                        UpdatedAtUtc = conversation.UpdatedAtUtc
                    })
                .ToList()
        };

        if (conversationId.HasValue)
        {
            AiConversationDto? conversation =
                await _aiConversationService.GetAsync(
                    conversationId.Value,
                    id,
                    cancellationToken);

            if (conversation is null)
            {
                return NotFound();
            }

            viewModel.Messages = conversation.Messages
                .Select(message =>
                    new AiAgentTestMessageViewModel
                    {
                        Role = message.Role,
                        Content = message.Content
                    })
                .ToList();
        }

        return View(viewModel);
    }
    [HttpPost("{id:guid}/testar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(
        Guid id,
        AiAgentTestViewModel form,
        CancellationToken cancellationToken)
    {
        AiAgentDetailsDto? agent =
            await _aiAgentService.GetAsync(id, cancellationToken);

        if (agent is null)
        {
            return NotFound();
        }

        form.AgentId = agent.Id;
        form.AgentName = agent.Name;
        form.AgentDescription = agent.Description;
        form.Messages ??= new List<AiAgentTestMessageViewModel>();

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        string userMessage = form.Message.Trim();

        OperationResult<AiAgentRunResult> result =
            await _aiAgentRunner.RunAsync(
                id,
                userMessage,
                form.ConversationId,
                cancellationToken);

        if (result.Succeeded && result.Value is not null)
        {
            return RedirectToAction(
                nameof(Test),
                new
                {
                    id,
                    conversationId = result.Value.ConversationId
                });
        }
        else
        {
            form.ErrorMessage = string.Join(" ", result.Errors);
        }

        return View(form);
    }
    private Guid GetTenantId()
    {
        string? value = User.FindFirstValue(OrizonClaimTypes.TenantId);

        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException(
                "Usuário autenticado sem tenant.");
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}














