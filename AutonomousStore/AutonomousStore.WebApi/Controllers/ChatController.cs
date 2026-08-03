using AutonomousStore.WebApi.Contracts.Chat;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

// Sem [Authorize]: o assistente de dúvidas fica disponível mesmo pra quem ainda não tem cadastro.
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ChatController : ControllerBase
{
    private readonly IGeminiChatService _chatService;

    public ChatController(IGeminiChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Send(ChatRequest request, CancellationToken cancellationToken)
    {
        if (request.Messages.Count == 0)
            return BadRequest(new { error = "Envie ao menos uma mensagem." });

        var reply = await _chatService.GetReplyAsync(request.Messages, cancellationToken);
        return Ok(new ChatResponse(reply));
    }
}
