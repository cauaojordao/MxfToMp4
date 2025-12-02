using Application.Interfaces.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Mediator;

/// <summary>
/// Implementação do padrão Mediator para envio de comandos, consultas e publicação de notificações.
/// Utiliza injeção de dependência para resolver handlers e behaviors dinamicamente.
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="Mediator"/>.
    /// </summary>
    /// <param name="serviceProvider">Provedor de serviços para resolução de dependências.</param>
    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Envia um request para o handler correspondente, executando behaviors do pipeline se existirem.
    /// </summary>
    /// <typeparam name="TResponse">Tipo de resposta esperada.</typeparam>
    /// <param name="request">Request a ser processado.</param>
    /// <param name="cancellationToken">Token de cancelamento opcional.</param>
    /// <returns>Resposta do handler.</returns>
    /// <exception cref="ArgumentNullException">Se o request for nulo.</exception>
    /// <exception cref="InvalidOperationException">Se não houver handler registrado para o request.</exception>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // Identifica o tipo do request e do handler
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

        // Cria escopo para resolução de dependências
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService(handlerType);

        if (handler == null)
            throw new InvalidOperationException($"Handler não encontrado para o request: {requestType.Name}");

        // Obtém método Handle do handler
        var handleMethod = handlerType.GetMethod("Handle");

        // Delegate para execução do handler
        RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken });

        // Executa handler (com behaviors, se existirem)
        return await handlerDelegate();
    }

    /// <summary>
    /// Publica uma notificação para todos os handlers registrados.
    /// </summary>
    /// <typeparam name="TNotification">Tipo da notificação.</typeparam>
    /// <param name="notification">Notificação a ser publicada.</param>
    /// <param name="cancellationToken">Token de cancelamento opcional.</param>
    /// <exception cref="ArgumentNullException">Se a notificação for nula.</exception>
    public async Task Publish<TNotification>(TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));

        var notificationType = typeof(TNotification);
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        // Executa todos os handlers da notificação
        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod("Handle");
            var task = (Task)method.Invoke(handler, new object[] { notification, cancellationToken });
            await task;
        }
    }
}