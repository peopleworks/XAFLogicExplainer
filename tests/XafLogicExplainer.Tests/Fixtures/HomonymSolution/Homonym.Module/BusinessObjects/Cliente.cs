using System.ComponentModel.DataAnnotations;

namespace Homonym.Core;

/// <summary>
/// The module's own Cliente, shadowing the library's. Its shape is the one that is real here.
/// </summary>
public class Cliente
{
    [Key]
    public int Id { get; set; }

    public string Nombre { get; set; }

    public string Correo { get; set; }
}
