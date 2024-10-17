using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.PopupWindows
{
    public class EnableBasicEncryptionPopup : PopupWindowContent
    {
        private readonly float _windowWidth;
        private readonly GUIStyle _titleStyle;
        
        public EnableBasicEncryptionPopup(float width)
        {
            _windowWidth = width;
            _titleStyle = new GUIStyle("label")
            {
                fontSize = 18,
            };
        }

        private string _password = "";

        public override Vector2 GetWindowSize() => new(_windowWidth, 162);
        
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
            
            GUI.Label(Consume(_titleStyle.fontSize), "Disable Master-Password Protection", _titleStyle);
            Consume(8);
            GUI.Label(Consume(18), "Password");
            _password = GUI.PasswordField(Consume(18), _password, '•');
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
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_password)))
                if (GUI.Button(confirmRect, "Confirm"))
                {
                    var passwordEncryption = new PasswordEncryption();
                    if (!passwordEncryption.Unlock(_password))
                    {
                        _password = "";
                        GUI.FocusControl(null);
                        EditorUtility.DisplayDialog("Easy Login", "Failed to unlock vault. Please try again. If the problem persists, try different passwords or contact us.", "OK");
                    }
                    else
                    {
                        Config.ChangeEncryptionMethod(
                            passwordEncryption,
                            new BasicEncryption()
                        );
                        editorWindow.Close();
                        Log.Info("Successfully switched to basic encryption");
                    }
                }
            
            Consume(10);
            var lineRect = Consume(1);
            var lineGradient = EditorGUIUtility.isProSkin
                ? 0.368627451f
                : 0.388235294f;
            Handles.color = new Color(lineGradient, lineGradient, lineGradient);
            Handles.DrawLine(new Vector3(lineRect.x, lineRect.y), new Vector3(lineRect.xMax, lineRect.y));
            Consume(10);
            if (GUI.Button(Consume(24), "Forgot my password / Reset vault"))
            {
                if (EditorUtility.DisplayDialog(
                    "Easy Login",
                    "This will remove all users stored in the vault and remove the password protection.\n\nAre you sure you want to continue?",
                    "Yes, reset vault",
                    "No, cancel"
                )) {
                        var userIds = Config.GetAccounts().Select(a => a.Id).ToList();
                        userIds.ForEach(Config.RemoveAccount);
                        Config.ChangeEncryptionMethod(
                            Accounts.KeyringManager.EncryptionLayer,
                            new BasicEncryption()
                        );
                        Log.Info("Successfully reset vault");
                }
            }
        }
    }
}