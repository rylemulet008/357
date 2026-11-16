using Shop357.UI;

namespace Shop357;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI", 10f));

        // Show the login screen; after "Log out" the loop shows it again.
        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }

            var main = new MainForm();
            Application.Run(main);
            if (!main.LoggedOut) return;
        }
    }
}
