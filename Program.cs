#region Resharper
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
#endregion

#region Unlicense
/* This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to https://unlicense.org */
#endregion

#region References
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
#endregion

namespace VRCXPlus
{
    internal static class Patch
    {
        private static readonly Dictionary<string, string[]> LanguagePatches = new()
        {
            {
                "en, fr, ko, vi", new[]
                {
                    @"Local Favorites \(Requires VRC\+\)",
                    "Local Favorites"
                }
            },
            {
                "es", new[]
                {
                    @"Favoritos locales \(Requiere VRC\+\)",
                    "Favoritos locales"
                }
            },
            {
                "ja", new[]
                {
                    @"ローカルのお気に入り \(VRC\+が必要\)",
                    "ローカルのお気に入り"
                }
            },
            {
                "pl", new[]
                {
                    @"Lokalne ulubione \(wymaga VRC\+\)",
                    "Lokalne ulubione"
                }
            },
            {
                "pt", new[]
                {
                    @"Favoritos Locais \(Requer VRC\+\)",
                    "Favoritos Locais"
                }
            },
            {
                "ru", new[]
                {
                    @"Локальное избранное \(требуется VRC\+\)",
                    "Локальное избранное"
                }
            },
            {
                "zh-cn", new[]
                {
                    @"本地收藏（需要 VRC\+，游戏内不可见）",
                    "本地收藏"
                }
            },
            {
                "zh-tw", new[]
                {
                    @"本地收藏列表 \(需要 VRC\+\)",
                    "本地收藏列表"
                }
            }
        };
        
        public static void Main()
        {
            Console.Title = "VRCX+";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WaitForMsg("This patch was only intended for Windows!");
                return;
            }

            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                WaitForMsg("This patch requires administrator access to ensure it works!");
                return;
            }

            var vrcx = Process.GetProcessesByName("VRCX");
            var basePath = "NOT FOUND";
            var dir = "NOT FOUND";

            if (vrcx.Length == 0)
            {
                if (!WaitForChoice("Could not find VRCX, continue install? (y/n)"))
                {
                    WaitForMsg("Ensure VRCX is running to auto detect it!");
                    return;
                }
            }
            else
            {
                basePath = Path.GetDirectoryName(vrcx[0].MainModule?.FileName);
                dir = $@"{basePath}\html\app.js";
            }
            
            while (!File.Exists(dir))
            {
                Console.WriteLine("Could not locate app.js, are we editing VRCX?");
                Console.WriteLine($"[BASE PATH] {basePath}");

                if (!WaitForChoice("Override this path and continue install? (y/n)"))
                {
                    WaitForMsg("Ensure no other running apps are named VRCX when running this patch!");
                    return;
                }

                Console.WriteLine("Input override base path!");
                basePath = Console.ReadLine();
                dir = @$"{basePath}\html\app.js";
            }

            if (vrcx.Length > 0)
                vrcx[0].Kill();
            
            var code = File.ReadAllText(dir);
            
            Console.WriteLine("Attempting to patch functions!");
            var genericFailedMsg =
                "Could not find original function VRCX has changed substantially or the patch was already applied! Please create an Issue if it is the former.";
            var obfuscated = Regex.Matches(code, @"([a-zA-Z])\.methods\.");
            if (RegexPatch(ref code,
                    @"[a-zA-Z]\.methods\.isLocalUserVrcplusSupporter=function\(\){return [a-zA-Z]\.currentUser\.\$isVRCPlus}",
                    $"{obfuscated[0].Value}isLocalUserVrcplusSupporter=function(){{return true}}"))
                Console.WriteLine("Patched function, local favorites are always on!");
            else
            {
                WaitForMsg(genericFailedMsg);
                return;
            }
            
            var obfuscatedQuery = Regex.Matches(code, @"([a-zA-Z])\.length>=3");

            if (RegexPatch(ref code,
                    @"\)&&this\.avatarRemoteDatabase&&[a-zA-Z]\.length>=3\)",
                    $")&&this.avatarRemoteDatabase&&{obfuscatedQuery[0].Value.Replace('3', '1')})"))
                Console.WriteLine("Patched function, avatar searching min length set to 1!");
            else
            {
                WaitForMsg(genericFailedMsg);
                return;
            }

            Console.WriteLine("Attempting to patch languages!");
            foreach (var lang in LanguagePatches)
            {
                for (int i = 0; i < lang.Value.Length / 2; i++)
                {
                    Console.WriteLine(RegexPatch(ref code, lang.Value[i], lang.Value[i + 1])
                        ? $"Patched {lang.Key}!"
                        : $"Failed to patch {lang.Key}, this could be due to Stable/Nightly discrepancies!");
                }
            }

            File.WriteAllText(dir, code);
            Process.Start($@"{basePath}\VRCX.exe");
            WaitForMsg("Successfully patched VRCX!");
        }

        private static void WaitForMsg(object reason)
        {
            Console.WriteLine(reason);
            Console.ReadKey();
        }

        private static bool WaitForChoice(object reason)
        {
            Console.WriteLine(reason);
            var response = Console.ReadKey();
            Console.WriteLine();
            return response.KeyChar switch
            {
                'Y' => true,
                'y' => true,
                _ => false
            };
        }

        private static bool RegexPatch(ref string original, string regex, string patch)
        {
            var funcReg = new Regex(regex);
            if (!funcReg.IsMatch(original)) return false;
            original = funcReg.Replace(original, patch);
            return true;
        }
    }
}
