using Specter.Rules;
using System.Runtime.CompilerServices;

[assembly: RuleCollection(Name = "PS")]
[assembly: InternalsVisibleTo("Specter")]
[assembly: InternalsVisibleTo("Specter.PssaCompatibility")]
[assembly: InternalsVisibleTo("Specter.Test")]
