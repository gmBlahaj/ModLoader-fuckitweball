using System.Reflection;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.utils;
using Newtonsoft.Json;
using RSG;
using UnityEngine;

namespace NeoModLoader.services;

[Experimental]
internal static class ModWorkshopService
{
    internal static Promise steamWorkshopPromise;
    private static IPlatformSpecificModWorkshopService workshopServiceBackend;

    public static void Init()
    {
        steamWorkshopPromise = RF.GetStaticField<Promise, SteamSDK>("steamInitialized");
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            workshopServiceBackend = new ModWorkshopServiceWindows();
        }
        else
        {
            workshopServiceBackend = new ModWorkshopServiceUnix();
        }
    }

    private static void UploadModLoader(string changelog)
    {
        workshopServiceBackend.UploadModLoader(changelog);
    }

    /// <summary>
    /// Try to Upload a mod to Steam Workshop
    /// </summary>
    public static Promise UploadMod(IMod mod, string changelog, bool verified = false)
    {
        ModDeclare mod_decl = mod.GetDeclaration();
        string name = mod_decl.Name;
        string aiDisclosureText = $"\n[AI Disclosure: {mod_decl.GetAIAttributionDisplay()}]";
        string description = $"{name} Uploaded by NeoModLoader\n" +
                             $"{name} 由NeoModLoader上传\n" +
                             $"{aiDisclosureText}\n\n" +
                             $"{mod_decl.Description}\n\n" +
                             $"ModLoader: {CoreConstants.RepoURL}\n\n" +
                             $"模组加载器: {CoreConstants.RepoURL}";
        string workshopPath = Path.Combine(SaveManager.generateMainPath("workshop_upload_mod") + mod_decl.UID);
        if (Directory.Exists(workshopPath))
        {
            Directory.Delete(workshopPath, true);
        }

        if (!Directory.Exists(SaveManager.generateMainPath("workshop_upload_mod")))
        {
            Directory.CreateDirectory(SaveManager.generateMainPath("workshop_upload_mod"));
        }

        Directory.CreateDirectory(workshopPath);
        // Prepare files to upload
        List<string> files_to_upload = SystemUtils.SearchFileRecursive(mod_decl.FolderPath,
            (filename) =>
            {
                // To ignore .git and .vscode and so on files
                return !filename.StartsWith(".");
            },
            (dirname) =>
            {
                // To ignore .git and .vscode and so on files
                return !dirname.StartsWith(".") && !Paths.IgnoreSearchDirectories.Contains(dirname);
            });
        foreach (string file_full_path in files_to_upload)
        {
            string path = Path.Combine(workshopPath,
                file_full_path.Replace(mod_decl.FolderPath, "").Replace("\\", "/").Substring(1));

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            File.Copy(file_full_path, path);
        }

        string previewImagePath;
        if (string.IsNullOrEmpty(mod_decl.IconPath))
        {
            using Stream icon_stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NeoModLoader.resources.logo.png");
            using FileStream icon_file = File.Create(Path.Combine(workshopPath, "preview.png"));
            icon_stream.Seek(0, SeekOrigin.Begin);
            icon_stream.CopyTo(icon_file);
            previewImagePath = Path.Combine(workshopPath, "preview.png");
        }
        else
        {
            previewImagePath = Path.Combine(workshopPath, mod_decl.IconPath);
        }

        previewImagePath = ApplyAIBadgeToThumbnail(previewImagePath, mod_decl.AIAttribution, workshopPath);

        // This works for BepInEx mods
        File.WriteAllText(Path.Combine(workshopPath, "mod.json"), JsonConvert.SerializeObject(mod_decl, Formatting.Indented));

        return workshopServiceBackend.UploadMod(name, description, previewImagePath, workshopPath, changelog, verified);
    }

    public static Promise TryEditMod(ulong fileID, IMod mod, string changelog)
    {
        ModDeclare mod_decl = mod.GetDeclaration();
        string workshopPath = Path.Combine(SaveManager.generateMainPath("workshop_upload_mod") + mod_decl.UID);
        if (Directory.Exists(workshopPath))
        {
            Directory.Delete(workshopPath, true);
        }

        if (!Directory.Exists(SaveManager.generateMainPath("workshop_upload_mod")))
        {
            Directory.CreateDirectory(SaveManager.generateMainPath("workshop_upload_mod"));
        }

        Directory.CreateDirectory(workshopPath);
        // Prepare files to upload
        List<string> files_to_upload = SystemUtils.SearchFileRecursive(mod_decl.FolderPath,
            (filename) =>
            {
                // To ignore .git and .vscode and so on files
                return !filename.StartsWith(".");
            },
            (dirname) =>
            {
                // To ignore .git and .vscode and so on files
                return !dirname.StartsWith(".") && !Paths.IgnoreSearchDirectories.Contains(dirname);
            });
        foreach (string file_full_path in files_to_upload)
        {
            string path = Path.Combine(workshopPath,
                file_full_path.Replace(mod_decl.FolderPath, "").Replace("\\", "/").Substring(1));

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            File.Copy(file_full_path, path);
        }

        string previewImagePath;
        if (string.IsNullOrEmpty(mod_decl.IconPath))
        {
            using Stream icon_stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NeoModLoader.resources.logo.png");
            using FileStream icon_file = File.Create(Path.Combine(workshopPath, "preview.png"));
            icon_stream.Seek(0, SeekOrigin.Begin);
            icon_stream.CopyTo(icon_file);
            previewImagePath = Path.Combine(workshopPath, "preview.png");
        }
        else
        {
            previewImagePath = Path.Combine(workshopPath, mod_decl.IconPath);
        }

        previewImagePath = ApplyAIBadgeToThumbnail(previewImagePath, mod_decl.AIAttribution, workshopPath);

        File.WriteAllText(Path.Combine(workshopPath, "mod.json"), JsonConvert.SerializeObject(mod_decl, Formatting.Indented));

        return workshopServiceBackend.EditMod(fileID, previewImagePath, workshopPath, changelog);
    }

    private static string ApplyAIBadgeToThumbnail(string originalPreviewPath, string attribution, string workshopPath)
    {
        if (string.IsNullOrEmpty(originalPreviewPath) || !File.Exists(originalPreviewPath))
        {
            return originalPreviewPath;
        }

        string resourceName = attribution switch
        {
            "ai_made" => "NeoModLoader.resources.ai_badge_made.png",
            "ai_assisted" => "NeoModLoader.resources.ai_badge_assisted.png",
            "not_ai" => "NeoModLoader.resources.ai_badge_not_ai.png",
            _ => "NeoModLoader.resources.ai_badge_not_ai.png"
        };

        try
        {
            byte[] badgeBytes = null;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (s != null)
                {
                    badgeBytes = new byte[s.Length];
                    s.Read(badgeBytes, 0, badgeBytes.Length);
                }
            }

            if (badgeBytes == null || badgeBytes.Length == 0)
            {
                return originalPreviewPath;
            }

            byte[] baseBytes = File.ReadAllBytes(originalPreviewPath);
            Texture2D baseTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!baseTex.LoadImage(baseBytes))
            {
                return originalPreviewPath;
            }

            Texture2D badgeTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!badgeTex.LoadImage(badgeBytes))
            {
                return originalPreviewPath;
            }

            RenderTexture rt = RenderTexture.GetTemporary(baseTex.width, baseTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(baseTex, rt);
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D resultTex = new Texture2D(baseTex.width, baseTex.height, TextureFormat.RGBA32, false);
            resultTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            resultTex.Apply();

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(rt);

            int badgeWidth = Mathf.Clamp(resultTex.width / 3, 60, 240);
            int badgeHeight = Mathf.RoundToInt((float)badgeWidth / badgeTex.width * badgeTex.height);

            Texture2D scaledBadge = scaleTexture(badgeTex, badgeWidth, badgeHeight);

            int startX = Mathf.Max(0, resultTex.width - scaledBadge.width - 5);
            int startY = 5;

            for (int x = 0; x < scaledBadge.width; x++)
            {
                for (int y = 0; y < scaledBadge.height; y++)
                {
                    int targetX = startX + x;
                    int targetY = startY + y;
                    if (targetX >= resultTex.width || targetY >= resultTex.height) continue;

                    Color badgeColor = scaledBadge.GetPixel(x, y);
                    if (badgeColor.a > 0.01f)
                    {
                        Color baseColor = resultTex.GetPixel(targetX, targetY);
                        Color blendedColor = Color.Lerp(baseColor, badgeColor, badgeColor.a);
                        resultTex.SetPixel(targetX, targetY, blendedColor);
                    }
                }
            }
            resultTex.Apply();

            byte[] taggedPng = resultTex.EncodeToPNG();
            string outPath = Path.Combine(workshopPath, "preview_tagged.png");
            File.WriteAllBytes(outPath, taggedPng);
            return outPath;
        }
        catch (Exception e)
        {
            LogService.LogWarning($"Failed to composite AI badge on thumbnail: {e.Message}");
            return originalPreviewPath;
        }
    }

    private static Texture2D scaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    public static void FindSubscribedMods()
    {
        workshopServiceBackend.FindSubscribedMods();
    }

    public static ModDeclare GetNextModFromWorkshopItem()
    {
        return workshopServiceBackend.GetNextModFromWorkshopItem();
    }
}