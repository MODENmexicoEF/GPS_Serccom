using MahApps.Metro.ViewModels;

namespace MahApps.Metro.Models
{
    public class EmailRecipient : ObservableObject
    {
        public int Id { get; set; }

        public string Email { get; set; } = "";

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}