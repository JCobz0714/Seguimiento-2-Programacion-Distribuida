namespace SGCAE.Application.Credentials.Commands;

public sealed record BlockCredentialCommand(Guid Id);

public sealed class BlockCredentialHandler(ICredentialRepository repository)
{
    public Task<CredentialDto> HandleAsync(BlockCredentialCommand command, CancellationToken cancellationToken = default) =>
        repository.ExecuteAsync(async token =>
        {
            var credential = await repository.GetForUpdateAsync(command.Id, token)
                ?? throw new CredentialNotFoundException(command.Id);
            credential.Block();
            await repository.SaveChangesAsync(token);
            return CredentialDto.From(credential);
        }, cancellationToken);
}
