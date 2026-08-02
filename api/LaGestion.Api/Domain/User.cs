namespace LaGestion.Api.Domain;

/// <summary>Rôle applicatif d'un compte.</summary>
public enum UserRole
{
    /// <summary>Prestataire indépendant, côté application mobile.</summary>
    Contractor,

    /// <summary>Membre de l'agence, côté back-office.</summary>
    Admin,

    /// <summary>Responsable de l'agence : administration plus facturation.</summary>
    Owner,
}

/// <summary>Compte permettant de se connecter à l'une des deux interfaces.</summary>
public class User : AgencyOwnedEntity
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}
