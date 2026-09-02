using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Vision;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

// Sem [Authorize]: esse endpoint simula o módulo de Hardware (câmera olhando a prateleira),
// igual ao POST /api/sessions/{id}/items — não é uma ação feita diretamente pelo cliente logado.
[ApiController]
[Route("api/vision")]
[AllowAnonymous]
public class VisionController : ControllerBase
{
    private readonly IGeminiVisionService _visionService;
    private readonly IStoreSessionRepository _sessionRepository;
    private readonly IProductRepository _productRepository;
    private readonly IRegistradorDeOcorrencia _ocorrencias;

    public VisionController(
        IGeminiVisionService visionService,
        IStoreSessionRepository sessionRepository,
        IProductRepository productRepository,
        IRegistradorDeOcorrencia ocorrencias)
    {
        _visionService = visionService;
        _sessionRepository = sessionRepository;
        _productRepository = productRepository;
        _ocorrencias = ocorrencias;
    }

    /// <summary>
    /// Chamado pela câmera de uma prateleira específica. Não recebe Id de sessão — a câmera não
    /// sabe (e não deveria saber) quem está comprando, só quais produtos ela vigia. O sistema
    /// resolve sozinho qual sessão está aberta no momento pra aplicar a mudança.
    /// </summary>
    [HttpPost("detect-shelf-change")]
    public async Task<ActionResult<DetectShelfChangeResponse>> DetectShelfChange(
        DetectShelfChangeRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetCurrentOpenSessionAsync(cancellationToken);

        if (session is null)
            return Ok(new DetectShelfChangeResponse("nenhuma", null, "Nenhuma sessão aberta no momento — ninguém está comprando na loja agora."));

        var allProducts = await _productRepository.GetAllAsync(cancellationToken);

        var candidateProducts = request.ProductIds.Count > 0
            ? allProducts.Where(p => request.ProductIds.Contains(p.Id) && p.IsActive).ToList()
            : allProducts.Where(p => p.IsActive).ToList();

        if (candidateProducts.Count == 0)
            return BadRequest(new { error = "Nenhum produto configurado pra essa prateleira." });

        var result = await _visionService.AnalyzeShelfChangeAsync(
            request.BeforeImageBase64,
            request.AfterImageBase64,
            candidateProducts,
            cancellationToken);

        if (result.Action == "nenhuma" || string.IsNullOrWhiteSpace(result.ProductName))
        {
            // FURO DE COBERTURA, e não crime. Há alguém na loja (a sessão está
            // aberta) e a câmera comparou antes/depois sem achar diferença.
            // Pode não ter saído nada — por isso a severidade é informativa e
            // ninguém é acusado. O valor está na SOMA: qual prateleira
            // acumula cegueira ao longo das semanas.
            await _ocorrencias.RegistrarAsync(
                Deteccoes.CameraNaoViuMudanca(session.Id, DateTime.UtcNow), cancellationToken);

            return Ok(new DetectShelfChangeResponse(result.Action, null, "Nenhuma mudança de produto detectada."));
        }

        var matchedProduct = candidateProducts.FirstOrDefault(p =>
            string.Equals(p.Name, result.ProductName, StringComparison.OrdinalIgnoreCase));

        matchedProduct ??= candidateProducts.FirstOrDefault(p =>
            p.Name.Contains(result.ProductName, StringComparison.OrdinalIgnoreCase)
            || result.ProductName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));

        if (matchedProduct is null)
        {
            // Um item saiu da prateleira e NÃO entrou em carrinho nenhum,
            // porque o sistema não sabe o que ele é. Some do estoque sem
            // venda e sem alarme — este é o caso que mais custa em silêncio.
            await _ocorrencias.RegistrarAsync(
                Deteccoes.ProdutoForaDoCatalogo(session.Id, result.ProductName, DateTime.UtcNow),
                cancellationToken);

            return Ok(new DetectShelfChangeResponse(
                result.Action,
                result.ProductName,
                $"O Gemini identificou \"{result.ProductName}\", mas não encontrei esse produto entre os monitorados."));
        }

        try
        {
            if (result.Action == "retirado")
            {
                session.AddItem(matchedProduct.Id, matchedProduct.Name, matchedProduct.Price);
                await _sessionRepository.SaveChangesAsync(cancellationToken);
                return Ok(new DetectShelfChangeResponse(result.Action, matchedProduct.Name, $"{matchedProduct.Name} adicionado ao carrinho."));
            }

            if (result.Action == "devolvido")
            {
                session.RemoveItem(matchedProduct.Id);
                await _sessionRepository.SaveChangesAsync(cancellationToken);
                return Ok(new DetectShelfChangeResponse(result.Action, matchedProduct.Name, $"{matchedProduct.Name} removido do carrinho."));
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // A sessão pode ter sido alterada por outra requisição entre a leitura e a gravação
            // (comum quando vários testes acontecem em sequência rápida). Não é erro do usuário —
            // só pede pra tentar de novo, em vez de quebrar com 500.
            return Ok(new DetectShelfChangeResponse("nenhuma", null, "Conflito momentâneo salvando a mudança — tenta de novo em instantes."));
        }

        return Ok(new DetectShelfChangeResponse("nenhuma", null, "Nenhuma mudança de produto detectada."));
    }
}