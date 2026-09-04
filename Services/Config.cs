using MCDSaveEdit.Data;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
#nullable enable

namespace MCDSaveEdit.Services
{
    public class Config
    {
        public static Config instance = new Config();

        public bool isConfigsReady { get; protected set; }

        public bool showInventoryIndexOrEquipmentSlot {
            get {
                // Internal inventory indexes and equipment-slot identifiers are
                // implementation details, not useful item-card metadata.  Do not
                // expose them merely because a developer is running a Debug build.
                return false;
            }
        }

        public bool showIDsInSelectionWindow {
            get {
                return false;
            }
        }

        public string? stableReleaseVersionString { get; protected set; }
        public string? betaReleaseVersionString { get; protected set; }

        public virtual async Task downloadAsync()
        {
            //Using GitHub
            try
            {
                var request = WebRequest.Create(Constants.LATEST_RELEASE_GITHUB_URL);
                using var response = await request.GetResponseAsync();
                stableReleaseVersionString = response.ResponseUri.Segments.Last();
                betaReleaseVersionString = string.Empty;
                isConfigsReady = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public virtual string newVersionDownloadURL()
        {
            return Constants.LATEST_RELEASE_GITHUB_URL;
        }

        public string versionLabel()
        {
            bool hasStableVersion = tryParseVersion(stableReleaseVersionString, out Version stableVersion);
            bool hasBetaVersion = tryParseVersion(betaReleaseVersionString, out Version betaVersion);

            if (hasStableVersion && Constants.CURRENT_VERSION.Equals(stableVersion))
            {
                return R.STABLE_VERSION_LABEL;
            }
            if (hasBetaVersion && Constants.CURRENT_VERSION.Equals(betaVersion))
            {
                return R.BETA_VERSION_LABEL;
            }
            if (hasBetaVersion && Constants.CURRENT_VERSION > betaVersion)
            {
                return R.UNRELEASED_VERSION_LABEL;
            }
            return R.OLD_VERSION_LABEL;
        }

        public bool isNewBetaVersionAvailable()
        {
            return tryParseVersion(betaReleaseVersionString, out Version betaVersion)
                && Constants.CURRENT_VERSION < betaVersion;
        }

        public bool isNewStableVersionAvailable()
        {
            return tryParseVersion(stableReleaseVersionString, out Version stableVersion)
                && Constants.CURRENT_VERSION < stableVersion;
        }

        private static bool tryParseVersion(string? value, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(value)) return false;

            string candidate = Uri.UnescapeDataString(value!).Trim().Trim('/');
            int slashIndex = candidate.LastIndexOf('/');
            if (slashIndex >= 0) candidate = candidate.Substring(slashIndex + 1);

            int firstDigit = 0;
            while (firstDigit < candidate.Length && !char.IsDigit(candidate[firstDigit])) firstDigit++;
            if (firstDigit >= candidate.Length) return false;

            candidate = candidate.Substring(firstDigit);
            int end = 0;
            while (end < candidate.Length && (char.IsDigit(candidate[end]) || candidate[end] == '.')) end++;
            candidate = candidate.Substring(0, end).TrimEnd('.');

            return Version.TryParse(candidate, out version);
        }

    }
}
