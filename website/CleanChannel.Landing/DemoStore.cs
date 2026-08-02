using System.Collections.Concurrent;

namespace CleanChannel.Landing;

public interface IDemoStore
{
    DemoUser? FindUserByEmail(string email);

    DemoUser? FindUserById(string userId);

    bool TryAddUser(DemoUser user);

    void AddLicense(DemoLicense license);

    DemoLicense? FindLicenseById(string licenseId);

    IEnumerable<DemoLicense> FindLicensesForUser(string userId);
}

public sealed class InMemoryDemoStore : IDemoStore
{
    private readonly ConcurrentDictionary<string, DemoUser> _usersById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _userIdsByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DemoLicense> _licensesById = new(StringComparer.Ordinal);

    public DemoUser? FindUserByEmail(string email) =>
        _userIdsByEmail.TryGetValue(email, out var userId) && _usersById.TryGetValue(userId, out var user)
            ? user
            : null;

    public DemoUser? FindUserById(string userId) =>
        _usersById.TryGetValue(userId, out var user) ? user : null;

    public bool TryAddUser(DemoUser user)
    {
        if (!_userIdsByEmail.TryAdd(user.Email, user.Id))
        {
            return false;
        }

        if (_usersById.TryAdd(user.Id, user))
        {
            return true;
        }

        _userIdsByEmail.TryRemove(user.Email, out _);
        return false;
    }

    public void AddLicense(DemoLicense license) => _licensesById[license.Id] = license;

    public DemoLicense? FindLicenseById(string licenseId) =>
        _licensesById.TryGetValue(licenseId, out var license) ? license : null;

    public IEnumerable<DemoLicense> FindLicensesForUser(string userId) =>
        _licensesById.Values.Where(license => license.OwnerUserId == userId);
}
