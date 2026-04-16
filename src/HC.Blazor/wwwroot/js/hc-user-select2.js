// HC user picker: Select2 4.x + Blazor Server (.NET helper for remote search).
// Requires jQuery and select2.full.min.js (see App.razor load order).
(function () {
    window.hcUserSelect2 = window.hcUserSelect2 || {};

    function parseBool(v) {
        return v === true || v === "true" || v === "True" || v === 1 || v === "1";
    }

    function fillOptions($el, items, multiselect) {
        $el.empty();
        if (!items || !items.length) {
            return;
        }
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var opt = new Option(item.text || "", item.id, true, true);
            if (item.html) {
                opt.setAttribute("data-hc-html", item.html);
            }
            $el.append(opt);
        }
        if (!multiselect && items.length > 1) {
            $el.find("option").slice(1).remove();
        }
    }

    window.hcUserSelect2.init = function (selectId, dotNetRef, options, initialItems) {
        var $el = $("#" + selectId);
        if (!$el.length) {
            return;
        }
        if ($el.data("select2")) {
            $el.off(".hcUserSelect2");
            $el.select2("destroy");
        }

        var multiselect = parseBool(options.multiselect);
        var placeholder = options.placeholder || "";

        fillOptions($el, initialItems, multiselect);

        $el.select2({
            width: "100%",
            placeholder: placeholder,
            allowClear: !multiselect,
            multiple: multiselect,
            minimumInputLength: typeof options.minimumInputLength === "number" ? options.minimumInputLength : 0,
            escapeMarkup: function (markup) {
                return markup;
            },
            templateResult: function (data) {
                if (data.loading) {
                    return data.text;
                }
                if (data.html) {
                    return data.html;
                }
                if (data.element) {
                    var h = data.element.getAttribute("data-hc-html");
                    if (h) {
                        return h;
                    }
                }
                return data.text;
            },
            templateSelection: function (data) {
                if (!data.id) {
                    return data.text;
                }
                if (data.html) {
                    return data.html;
                }
                if (data.element) {
                    var h = data.element.getAttribute("data-hc-html");
                    if (h) {
                        return h;
                    }
                }
                return data.text;
            },
            ajax: {
                delay: 250,
                transport: function (params, success, failure) {
                    var term = (params.data && params.data.term) ? params.data.term : "";
                    var page = params.data && params.data.page ? params.data.page : 1;
                    dotNetRef.invokeMethodAsync("SearchAsync", term, page).then(success).catch(failure);
                },
                processResults: function (data /* from SearchAsync */, params) {
                    return {
                        results: data.results || [],
                        pagination: { more: parseBool(data.more) }
                    };
                }
            }
        });

        $el.data("hcUserSelect2DotNetRef", dotNetRef);

        $el.on("change.hcUserSelect2", function () {
            var vals = $el.val();
            var arr = Array.isArray(vals) ? vals : (vals ? [vals] : []);
            dotNetRef.invokeMethodAsync("OnSelectionChangeAsync", arr);
        });
    };

    // Replace options + selection from Blazor (e.g. modal reset). May fire change once.
    window.hcUserSelect2.setSelection = function (selectId, items, multiselect) {
        var $el = $("#" + selectId);
        if (!$el.length || !$el.data("select2")) {
            return;
        }
        multiselect = parseBool(multiselect);
        var dotNetRef = $el.data("hcUserSelect2DotNetRef");
        $el.off("change.hcUserSelect2");
        $el.empty();
        fillOptions($el, items, multiselect);
        $el.trigger("change");
        if (dotNetRef) {
            $el.on("change.hcUserSelect2", function () {
                var vals = $el.val();
                var arr = Array.isArray(vals) ? vals : (vals ? [vals] : []);
                dotNetRef.invokeMethodAsync("OnSelectionChangeAsync", arr);
            });
        }
    };

    window.hcUserSelect2.destroy = function (selectId) {
        var $el = $("#" + selectId);
        if (!$el.length) {
            return;
        }
        $el.removeData("hcUserSelect2DotNetRef");
        if ($el.data("select2")) {
            $el.off(".hcUserSelect2");
            $el.select2("destroy");
        }
    };
})();
