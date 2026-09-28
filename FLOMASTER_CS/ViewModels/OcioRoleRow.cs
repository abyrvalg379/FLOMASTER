namespace FLOMASTER.ViewModels
{
    /// <summary>
    /// Строка блока ROLES: кнопка с текущим значением роли. Клик открывает
    /// пикер colorspaces (дерево по family, стиль окна логов).
    /// </summary>
    public class OcioRoleRow : ViewModelBase
    {
        public string RoleName { get; init; } = "";

        private string _displayText = "";
        /// <summary>"scene_linear: acescg (config)" / "default_byte: raw (override)"</summary>
        public string DisplayText { get => _displayText; set => SetProperty(ref _displayText, value); }

        private bool _isOverridden;
        public bool IsOverridden { get => _isOverridden; set => SetProperty(ref _isOverridden, value); }
    }
}
