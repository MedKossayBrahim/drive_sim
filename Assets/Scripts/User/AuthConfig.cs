namespace DriveSim.User
{
    // Adresse du petit serveur de comptes.
    // Dans l'éditeur Unity, localhost suffit.
    // Avant un build iPhone ou Android, mets ici l'adresse affichée par `npm start`
    // (http://IP-DU-MAC:5080). Le téléphone et le Mac doivent être sur le même Wi-Fi.
    public static class AuthConfig
    {
        public const int Port = 5080;
        public const string EditorServerUrl = "http://localhost:5080";
        public const string PhoneServerUrl = "http://192.168.10.58:5080";

        public static string DefaultServerUrl
        {
            get
            {
#if UNITY_EDITOR
                return EditorServerUrl;
#else
                return PhoneServerUrl;
#endif
            }
        }
    }
}
