namespace CedarRisk.Domain.Common;

/// <summary>
/// Usage déclaré du véhicule — axe principal du barème RC.
/// </summary>
public enum UsageVehicule
{
    VehiculeTourisme,            // VP personnel
    VehiculeTourismeEntreprise,  // VP société
    Taxi,                        // Voiture de place / grande remise
    TransportPublicVoyageurs,    // TPV — autocar / autobus
    TransportMarchandises        // Utilitaire léger
}
