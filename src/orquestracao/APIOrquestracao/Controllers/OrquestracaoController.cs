using Microsoft.AspNetCore.Mvc;
using APIOrquestracao.Models;
using APIOrquestracao.Clients;
using APIOrquestracao.Tracing;
using System.Text.Json;

namespace APIOrquestracao.Controllers;

[ApiController]
[Route("[controller]")]
public class OrquestracaoController : ControllerBase
{
    private readonly ILogger<OrquestracaoController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContagemClient _contagemClient;

    public OrquestracaoController(ILogger<OrquestracaoController> logger,
        IConfiguration configuration, ContagemClient contagemClient)
    {
        _logger = logger;
        _configuration = configuration;
        _contagemClient = contagemClient;
    }

    [HttpGet]
    public async Task<ResultadoOrquestracao> Get()
    {
        var resultado = new ResultadoOrquestracao
        {
            Horario = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        using var activity1 = OpenTelemetryExtensions.ActivitySource
            .StartActivity("RequestApiContagemPostgres")!;
        var urlApiContagem = _configuration["ApiContagemPostgres"]!;
        resultado.ContagemPostgres =
            await _contagemClient.ObterContagemAsync(urlApiContagem);
        _logger.LogInformation($"Valor contagem Postgres: {resultado.ContagemPostgres!.ValorAtual} | {urlApiContagem}");
        activity1.SetTag("api", "APIContagemPostgres");        
        activity1.SetTag("url", urlApiContagem);
        activity1.SetTag("content", JsonSerializer.Serialize(resultado.ContagemPostgres));
        activity1.Stop();

        using var activity2 = OpenTelemetryExtensions.ActivitySource
            .StartActivity("RequestApiContagemMySql")!;
        urlApiContagem = _configuration["ApiContagemMySql"]!;
        resultado.ContagemMySql =
            await _contagemClient.ObterContagemAsync(urlApiContagem);
        _logger.LogInformation($"Valor contagem MySQL: {resultado.ContagemPostgres!.ValorAtual} | {urlApiContagem}");
        activity2.SetTag("api", "APIContagemMySql");        
        activity2.SetTag("url", urlApiContagem);
        activity2.SetTag("content", JsonSerializer.Serialize(resultado.ContagemMySql));
        activity2.Stop();

        using var activity3 = OpenTelemetryExtensions.ActivitySource
            .StartActivity("RequestApiContagemSqlServer")!;
        urlApiContagem = _configuration["ApiContagemSqlServer"]!;
        resultado.ContagemSqlServer =
            await _contagemClient.ObterContagemAsync(urlApiContagem);
        _logger.LogInformation($"Valor contagem SQL Server: {resultado.ContagemPostgres!.ValorAtual} | {urlApiContagem}");
        activity2.SetTag("api", "ApiContagemSqlServer");        
        activity2.SetTag("url", urlApiContagem);
        activity2.SetTag("content", JsonSerializer.Serialize(resultado.ContagemMySql));
        activity2.Stop();

        return resultado;
    }
}
