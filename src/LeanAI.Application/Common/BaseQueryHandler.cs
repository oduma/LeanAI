using MediatR;

namespace LeanAI.Application.Common;

public abstract class BaseQueryHandler<TQuery, TResult>(IMediator mediator)
    : IRequestHandler<TQuery, TResult>
    where TQuery : IRequest<TResult>
{
    protected readonly IMediator Mediator = mediator;

    public abstract Task<TResult> Handle(TQuery request, CancellationToken cancellationToken);
}
