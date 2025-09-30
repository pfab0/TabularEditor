using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Text;
using System.Windows.Forms;
using TabularEditor.UI;

namespace TabularEditor.UIServices
{
    public static class UpdateService
    {
        //public const string VERSION_MANIFEST_URL = "https://raw.githubusercontent.com/TabularEditor/TabularEditor/master/TabularEditor/version.txt";
        public const string DOWNLOAD_UPDATE_URL = "https://github.com/TabularEditor/TabularEditor/releases/latest";
        public const string GITHUB_RELEASES_LATEST_API = "https://api.github.com/repos/TabularEditor/TabularEditor/releases/latest";

        public static Version CurrentBuild { get; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public static Version AvailableBuild { get; private set; } = null;
        public static VersionCheckResult AvailableVersion { get; private set; } = VersionCheckResult.NoNewVersion;

        /// <summary>
        /// Checks online to see if an updated version is available.
        /// </summary>
        /// <param name="displayErrors">Set to true to display an error message in case the update check fails</param>
        /// <returns>True if a newer version of Tabular Editor is available, false otherwise</returns>
        public static VersionCheckResult Check(bool displayErrors = false)
        {
            using (new Hourglass())
            {
                AvailableVersion = InternalCheck(displayErrors);
            }
            return AvailableVersion;
        }

        private static VersionCheckResult InternalCheck(bool displayErrors)
        {
            try
            {
                AvailableBuild = GetLatestVersionFromGitHub();
                return CurrentBuild.DetermineUpdate(AvailableBuild);
            }
            catch (Exception ex)
            {
                if (displayErrors) MessageBox.Show(ex.Message, "Unable to check for updated versions", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return VersionCheckResult.Unknown;
            }
        }

        /// <summary>
        /// Query the GitHub Releases API for the latest release and return a parsed Version.
        /// Uses WebRequest and Newtonsoft.Json for robust parsing.
        /// </summary>
        private static Version GetLatestVersionFromGitHub()
        {
            var url = GITHUB_RELEASES_LATEST_API;
            var wr = WebRequest.CreateHttp(url);
            wr.Proxy = ProxyCache.GetProxy(url);
            wr.Timeout = 5000;
            // GitHub API requires a user-agent header
            wr.UserAgent = "TabularEditorUpdateChecker";
            wr.Accept = "application/vnd.github.v3+json";
            wr.Headers["X-GitHub-Api-Version"] = "2022-11-28";

            using (var response = wr.GetResponse())
            using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                var json = reader.ReadToEnd();
                var obj = JObject.Parse(json);
                var tag = (string)obj["tag_name"];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    return null;
                }

                tag = tag.Trim();

                //if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                //{
                //    tag = tag.Substring(1);
                //}
                //// Extract the first contiguous sequence of digits and dots (e.g. 2.27.0 or 2.27.8878.22493)
                //var verMatch = System.Text.RegularExpressions.Regex.Match(tag, "\\d+(?:\\.\\d+){0,3}");
                //if (!verMatch.Success) return null;
                //return Version.Parse(verMatch.Value);
                return Version.Parse(tag);
            }
        }

        public static void OpenDownloadPage()
        {
            System.Diagnostics.Process.Start(DOWNLOAD_UPDATE_URL);
        }
    }

    public enum VersionCheckResult
    {
        NoNewVersion,
        PatchAvailable,
        MinorAvailable,
        MajorAvailable,
        Unknown
    }

    public static class VersionCheckResultExtension
    {
        public static bool UpdateAvailable(this VersionCheckResult result, bool skipPatchUpdates = false)
        {
            switch (result)
            {
                case VersionCheckResult.PatchAvailable:
                    if (skipPatchUpdates)
                        return false;
                    else
                        return true;
                case VersionCheckResult.MinorAvailable:
                case VersionCheckResult.MajorAvailable:
                    return true;
                default:
                    return false;
            }
        }

        internal static VersionCheckResult DetermineUpdate(this Version current, Version available)
        {
            if (available.Major > current.Major)
            {
                return VersionCheckResult.MajorAvailable;
            }
            else if (available.Major == current.Major && available.Minor > current.Minor)
            {
                return VersionCheckResult.MinorAvailable;
            }
            else if (available > current)
            {
                return VersionCheckResult.PatchAvailable;
            }
            else
            {
                return VersionCheckResult.NoNewVersion;
            }
        }
    }
}
