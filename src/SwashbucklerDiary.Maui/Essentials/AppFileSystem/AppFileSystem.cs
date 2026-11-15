using SwashbucklerDiary.Rcl.Essentials;

namespace SwashbucklerDiary.Maui.Essentials
{
    public partial class AppFileSystem : Rcl.Essentials.AppFileSystem
    {
        private static IAppFileSystem? defaultImplementation;

        public static IAppFileSystem Default
            => defaultImplementation ??= new AppFileSystem();

        public override string AppDataDirectory => FileSystem.Current.AppDataDirectory;

        public override string CacheDirectory => FileSystem.Current.CacheDirectory;
    }
}
