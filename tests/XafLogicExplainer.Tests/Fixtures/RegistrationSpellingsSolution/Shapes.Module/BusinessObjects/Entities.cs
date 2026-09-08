namespace Shapes.BusinessObjects;

/// <summary>Registered through a fully qualified <c>DbSet</c> property type.</summary>
public class Alpha { public int Id { get; set; } }

/// <summary>Registered through a <c>global::</c>-qualified type argument.</summary>
public class Beta { public int Id { get; set; } }

/// <summary>Registered only by a fluent mapping call, which is not read.</summary>
public class Gamma { public int Id { get; set; } }

/// <summary>Registered through an expression-bodied <c>DbSet</c> property.</summary>
public class Epsilon { public int Id { get; set; } }

/// <summary>Registered only as the type argument of the generic context base.</summary>
public class AppUser : Microsoft.AspNetCore.Identity.IdentityUser { public string Nick { get; set; } }

/// <summary>Registered only by a context outside the business-object folder, which is not read.</summary>
public class Zeta { public int Id { get; set; } }
