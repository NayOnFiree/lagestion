namespace LaGestion.Api.Domain;

/// <summary>
/// Compétence du référentiel. Le référentiel est propre à chaque agence :
/// chacune garde son vocabulaire métier.
/// </summary>
public class Skill : AgencyOwnedEntity
{
    public required string Name { get; set; }

    public ICollection<ContractorSkill> Contractors { get; set; } = [];
}

/// <summary>Association entre un prestataire et une compétence.</summary>
public class ContractorSkill : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    public Guid SkillId { get; set; }

    public Contractor? Contractor { get; set; }

    public Skill? Skill { get; set; }
}
