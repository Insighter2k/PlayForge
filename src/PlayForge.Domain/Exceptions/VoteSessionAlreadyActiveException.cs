namespace PlayForge.Domain.Exceptions;

public class VoteSessionAlreadyActiveException : DomainException
{
    public VoteSessionAlreadyActiveException(Guid groupId)
        : base($"Group {groupId} already has an active vote session.") { }
}
