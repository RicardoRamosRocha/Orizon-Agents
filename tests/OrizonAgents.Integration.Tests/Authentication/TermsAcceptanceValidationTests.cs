using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using OrizonAgents.Web.Models.Account;
using OrizonAgents.Web.Validation;

namespace OrizonAgents.Integration.Tests.Authentication;

public class TermsAcceptanceValidationTests
{
    [Fact]
    public void RegisterOrganizationViewModel_RejectsUncheckedAcceptedTerms()
    {
        var model = CreateValidModel(acceptedTerms: false);

        bool isValid = Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true);

        Assert.False(isValid);
    }

    [Fact]
    public void RegisterOrganizationViewModel_AcceptsCheckedAcceptedTerms()
    {
        var model = CreateValidModel(acceptedTerms: true);

        bool isValid = Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true);

        Assert.True(isValid);
    }

    [Fact]
    public async Task AcceptedTerms_BindsTrueWhenCheckboxPostsTrueBeforeHiddenFalse()
    {
        bool result = await BindAcceptedTermsAsync(new StringValues(new[] { "true", "false" }));

        Assert.True(result);
    }

    [Fact]
    public async Task AcceptedTerms_BindsFalseWhenOnlyHiddenFalseIsPosted()
    {
        bool result = await BindAcceptedTermsAsync(new StringValues("false"));

        Assert.False(result);
    }

    [Fact]
    public void MustBeTrueAttribute_EmitsClientValidationForCheckbox()
    {
        var attribute = new MustBeTrueAttribute { ErrorMessage = "Você precisa aceitar os termos." };
        var attributes = new Dictionary<string, string>();
        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterOrganizationViewModel),
            nameof(RegisterOrganizationViewModel.AcceptedTerms));
        var context = new ClientModelValidationContext(
            new ActionContext(),
            metadata,
            metadataProvider,
            attributes);

        attribute.AddValidation(context);

        Assert.Equal("true", attributes["data-val"]);
        Assert.Equal("Você precisa aceitar os termos.", attributes["data-val-mustbetrue"]);
    }

    private static async Task<bool> BindAcceptedTermsAsync(StringValues acceptedTermsValues)
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [nameof(RegisterOrganizationViewModel.AcceptedTerms)] = acceptedTermsValues
        });
        var valueProvider = new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture);
        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterOrganizationViewModel),
            nameof(RegisterOrganizationViewModel.AcceptedTerms));
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            new ActionContext { HttpContext = new DefaultHttpContext() },
            valueProvider,
            metadata,
            bindingInfo: null,
            modelName: nameof(RegisterOrganizationViewModel.AcceptedTerms));
        var binder = new SimpleTypeModelBinder(typeof(bool), NullLoggerFactory.Instance);

        await binder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        return Assert.IsType<bool>(bindingContext.Result.Model);
    }

    private static RegisterOrganizationViewModel CreateValidModel(bool acceptedTerms)
    {
        return new RegisterOrganizationViewModel
        {
            OrganizationName = "Orizon Test",
            Slug = "orizon-test",
            FullName = "Admin Orizon",
            Email = "admin@orizon.test",
            Password = AuthenticationTestFixture.ValidPassword,
            ConfirmPassword = AuthenticationTestFixture.ValidPassword,
            AcceptedTerms = acceptedTerms
        };
    }
}
