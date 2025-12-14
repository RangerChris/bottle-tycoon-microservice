namespace HeadquartersService.Services;

public interface IHeadquartersService
{
    Task ResetAsync();
    Task InitializeFleetAsync();
}