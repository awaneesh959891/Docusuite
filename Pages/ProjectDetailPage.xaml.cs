using MauiApp.Models;

#if WINDOWS
using MauiApp.Behaviors;
#endif

namespace MauiApp.Pages
{
    public partial class ProjectDetailPage : ContentPage
    {
        public ProjectDetailPage(ProjectDetailPageModel model)
        {
            InitializeComponent();

            BindingContext = model;
#if WINDOWS
    		ProjectTasksCollectionView.Behaviors.Add(new SingleSelectionKeyboardGuardBehavior());
    		IconsCollectionView.Behaviors.Add(new SingleSelectionKeyboardGuardBehavior());
#endif
        }
    }
}
