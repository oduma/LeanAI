using MediatR;

namespace LeanAI.Application.Common;

public abstract class BaseCommandHandler<TCommand, TResult>(IMediator mediator)
    : IRequestHandler<TCommand, TResult>
    where TCommand : IRequest<TResult>
{
    protected readonly IMediator Mediator = mediator;

    public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);
}
