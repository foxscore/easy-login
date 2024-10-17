using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.PopupWindows
{
    public class EnableMasterPasswordEncryptionPopup : PopupWindowContent
    {
        private readonly float _windowWidth;
        private readonly GUIStyle _titleStyle;
        
        public EnableMasterPasswordEncryptionPopup(float width)
        {
            _windowWidth = width;
            _titleStyle = new GUIStyle("label")
            {
                fontSize = 18,
            };
        }

        private string _password = "";
        private string _passwordConfirm = "";

        public override Vector2 GetWindowSize() => new(_windowWidth, 200);

        public override void OnGUI(Rect windowRect)
        {
            const int windowPadding = 10;
            windowRect.x += windowPadding;
            windowRect.width -= windowPadding * 2;
            windowRect.y += windowPadding;
            windowRect.height -= windowPadding * 2;

            Rect Consume(float height)
            {
                var newRect = new Rect(windowRect.x, windowRect.y, windowRect.width, height);
                windowRect.y += height;
                windowRect.height -= height;
                return newRect;
            }

            GUI.Label(Consume(_titleStyle.fontSize), "Enable Master-Password Protection", _titleStyle);
            Consume(8);
            GUI.Label(Consume(16), "You will have to enter your master-password every time you start Unity");
            GUI.Label(Consume(16), "in order to unlock access to your accounts.");
            Consume(8);
            GUI.Label(Consume(18), "Password");
            _password = GUI.PasswordField(Consume(18), _password, '•');
            Consume(8);
            GUI.Label(Consume(18), "Confirm password");
            _passwordConfirm = GUI.PasswordField(Consume(18), _passwordConfirm, '•');
            Consume(8);
            var buttonRect = Consume(25);
            var cancelRect = new Rect(buttonRect)
            {
                width = (buttonRect.width - 2) / 2
            };
            var confirmRect = new Rect(cancelRect)
            {
                x = cancelRect.xMax + 2
            };
            if (GUI.Button(cancelRect, "Cancel"))
                editorWindow.Close();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_password) || _password != _passwordConfirm))
                if (GUI.Button(confirmRect, "Confirm"))
                {
                    var passwordEncryption = new PasswordEncryption();
                    passwordEncryption.Setup(_password);
                    Config.ChangeEncryptionMethod(
                        Accounts.KeyringManager.EncryptionLayer,
                        passwordEncryption
                    );
                    editorWindow.Close();
                    Log.Info("Successfully switched to password encryption");
                }
        }
    }
}