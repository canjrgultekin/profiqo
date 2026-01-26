using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Users;

public sealed class User : Entity<UserId>
{
    private readonly List<UserRole> _roles = new();

    public TenantId TenantId { get; private set; }
    public EmailAddress Email { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private User() { }

    private User(
        UserId id,
        TenantId tenantId,
        EmailAddress email,
        string passwordHash,
        string displayName,
        IEnumerable<UserRole> roles)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;

        displayName = (displayName ?? string.Empty).Trim();
        if (displayName.Length > 200)
            throw new ArgumentException("DisplayName too long.", nameof(displayName));

        DisplayName = displayName;

        _roles.AddRange(roles.Distinct());

        if (_roles.Count == 0)
            throw new ArgumentException("User must have at least one role.", nameof(roles));

        Status = UserStatus.Active;

        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public static User CreateOwner(
        UserId id,
        TenantId tenantId,
        EmailAddress email,
        string passwordHash,
        string displayName)
    {
        return new User(id, tenantId, email, passwordHash, displayName, new[] { UserRole.Owner });
    }

    public static User Create(
        UserId id,
        TenantId tenantId,
        EmailAddress email,
        string passwordHash,
        string displayName,
        IEnumerable<UserRole> roles)
    {
        return new User(id, tenantId, email, passwordHash, displayName, roles);
    }

    public void Disable()
    {
        Status = UserStatus.Disabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AssignRole(UserRole role)
    {
        if (_roles.Contains(role)) return;
        _roles.Add(role);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void RemoveRole(UserRole role)
    {
        if (role == UserRole.Owner)
            throw new InvalidOperationException("Owner role cannot be removed.");

        _roles.Remove(role);

        if (_roles.Count == 0)
            throw new InvalidOperationException("User must have at least one role.");

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
