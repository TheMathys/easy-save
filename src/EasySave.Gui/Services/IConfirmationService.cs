using System.Threading.Tasks;

namespace EasySave.Gui.Services;

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string title, string message);
}
