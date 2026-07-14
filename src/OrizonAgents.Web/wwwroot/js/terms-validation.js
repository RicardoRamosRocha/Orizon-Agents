(function ($) {
  if (!$ || !$.validator || !$.validator.unobtrusive) {
    return;
  }

  $.validator.addMethod("mustbetrue", function (_value, element) {
    return element.checked === true;
  });

  $.validator.unobtrusive.adapters.addBool("mustbetrue");
})(window.jQuery);
