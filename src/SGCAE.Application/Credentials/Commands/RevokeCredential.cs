namespace SGCAE.Application.Credentials.Commands;

public sealed record RevokeCredentialCommand(Guid Id);

public sealed class RevokeCredentialHandler(ICredentialRepository repository)
{
    public Task<CredentialDto> HandleAsync(RevokeCredentialCommand command, CancellationToken cancellationToken = default) =>
        repository.ExecuteAsync(async token =>
        {
            var credential = await repository.GetForUpdateAsync(command.Id, token)
                ?? throw new CredentialNotFoundException(command.Id);
            credential.Revoke();
            await repository.SaveChangesAsync(token);
            return CredentialDto.From(credential);
        }, cancellationToken);
}
