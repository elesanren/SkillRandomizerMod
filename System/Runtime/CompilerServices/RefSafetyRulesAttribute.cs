// Decompiled with JetBrains decompiler
// Type: System.Runtime.CompilerServices.RefSafetyRulesAttribute
// Assembly: SkillRandomizerMod, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 31ECD94A-A255-405A-B0F7-6544B29C2F91
// Assembly location: E:\a\HardItemRandomizer\plugins\SkillRandomizerMod.dll

using Microsoft.CodeAnalysis;
using System.Runtime.InteropServices;

#nullable disable
namespace System.Runtime.CompilerServices;

[CompilerGenerated]
[Embedded]
[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
internal sealed class RefSafetyRulesAttribute : Attribute
{
  public readonly int Version;

  public RefSafetyRulesAttribute([In] int obj0) => this.Version = obj0;
}
