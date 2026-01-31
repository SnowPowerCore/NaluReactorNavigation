using Nalu;

namespace TestReactorApp.Components
{
    internal class HomePageState
    {
        public int Counter { get; set; }
    }

    partial class HomePage : Component<HomePageState>
    {
        [Inject]
        private readonly INavigationService _navigation;

        protected override void OnMounted()
        {
            MauiReactor.Routing.RegisterRoute<SecondPage>("page-2");
            base.OnMounted();
        }

        public override VisualNode Render() =>
            ContentPage(
                    ScrollView(
                        VStack(
                            Image("dotnet_bot.png")
                                .HeightRequest(200)
                                .HCenter()
                                .Set(SemanticProperties.DescriptionProperty, "Cute dot net bot waving hi to you!"),

                            Label("Hello World")
                                .FontSize(32)
                                .HCenter(),

                            Label("Welcome to MauiReactor: MAUI with superpowers!")
                                .FontSize(18)
                                .HCenter(),

                            Button("Navigate")
                                .OnClicked(NavigateToSecondPageAsync)
                                .HCenter()
                    )
                    .VCenter()
                    .Spacing(25)
                    .Padding(30, 0)
                )
            );

        private Task NavigateToSecondPageAsync() =>
            _navigation.GoToAsync(Nalu.Navigation.Relative().Push<SecondPage>());
    }
}