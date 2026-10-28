using System.Runtime.CompilerServices;

// Editör araçları (CSV içe aktarıcı, içerik oluşturucu) ve testler,
// ScriptableObject'lerin internal Initialize(...) metotlarını kullanabilsin.
[assembly: InternalsVisibleTo("BusTycoon.Editor")]
[assembly: InternalsVisibleTo("BusTycoon.Tests.EditMode")]
