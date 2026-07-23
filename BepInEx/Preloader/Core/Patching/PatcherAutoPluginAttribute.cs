// Decompiled with JetBrains decompiler
// Type: BepInEx.Preloader.Core.Patching.PatcherAutoPluginAttribute
// Assembly: SkillRandomizerMod, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 31ECD94A-A255-405A-B0F7-6544B29C2F91
// Assembly location: E:\a\HardItemRandomizer\plugins\SkillRandomizerMod.dll

using System;
using System.Diagnostics;

#nullable enable
namespace BepInEx.Preloader.Core.Patching;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
[Conditional("CodeGeneration")]
internal sealed class PatcherAutoPluginAttribute : Attribute
{
  public PatcherAutoPluginAttribute(string? id = null, string? name = null, string? version = null)
  {
  }
}
