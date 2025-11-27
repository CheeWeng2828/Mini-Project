// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;
});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;

    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

// Photo preview
$('.upload input').on('change', e => {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];

    img.dataset.src ??= img.src;

    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    }
    else {
        img.src = img.dataset.src;
        e.target.value = '';
    }

    // Trigger input validation
    $(e.target).valid();
});

// Image Slider

const slides = document.querySelectorAll(".slides img");
let slideIndex = 0;
let intervalId = null;

document.addEventListener("DOMContentLoaded", initializeSlider());
function initializeSlider() {

    if (slides.length > 0) {
        slides[slideIndex].classList.add("displaySlide");
        intervalId = setInterval(nextSlide, 5000); // Auto change next picture after 5 seconds
    }
}


function showSlide(index) {

    if (index >= slides.length) {
        slideIndex = 0;
    }
    else if (index < 0) {
        slideIndex = slides.length - 1;
    }

    slides.forEach(slide => {
        slide.classList.remove("displaySlide"); // Remove CSS displaySlide property
    });
    slides[slideIndex].classList.add("displaySlide");
}
function prevSlide() {
    clearInterval(intervalId)
    slideIndex--;
    showSlide(slideIndex)
}
function nextSlide() {
    clearInterval(intervalId)
    slideIndex++;
    showSlide(slideIndex)
}

/// Batch JS code
$(function () {
    // Select all / single row checkbox logic
    $(document).on('change', '#selectAll', function () {
        $('.row-checkbox').prop('checked', this.checked);
        toggleBulkActions();
    });

    $(document).on('change', '.row-checkbox', function () {
        updateSelectAllState();
        toggleBulkActions();
    });

    // Update "Select All" state
    function updateSelectAllState() {
        var total = $('.row-checkbox').length;
        var checked = $('.row-checkbox:checked').length;

        if (total === 0) {
            $('#selectAll').prop('checked', false).prop('indeterminate', false);
            return;
        }
        if (checked === total) {
            $('#selectAll').prop('checked', true).prop('indeterminate', false);
        } else if (checked === 0) {
            $('#selectAll').prop('checked', false).prop('indeterminate', false);
        } else {
            $('#selectAll').prop('checked', false).prop('indeterminate', true);
        }
    }

    // Get selected room IDs
    function getSelectedIds() {
        var ids = [];
        $('.row-checkbox:checked').each(function () { ids.push($(this).val()); });
        return ids;
    }

    // Show/hide bulk actions section
    function toggleBulkActions() {
        var cnt = getSelectedIds().length;
        if (cnt > 0) {
            $('#bulk-actions').show();
            $('#selected-count').text(cnt + ' selected');
        } else {
            $('#bulk-actions').hide();
        }
    }

    // Submit bulk action form (with AntiForgery token)
    function submitBulkAction(action, additionalData) {
        var selectedIds = getSelectedIds();
        var form = $('<form>', {
            'method': 'POST',
            'action': action
        });

        // Add selected IDs
        $.each(selectedIds, function (i, id) {
            form.append($('<input>', {
                'type': 'hidden',
                'name': 'selectedIds',
                'value': id
            }));
        });

        // Add extra data
        if (additionalData) {
            $.each(additionalData, function (name, value) {
                form.append($('<input>', {
                    'type': 'hidden',
                    'name': name,
                    'value': value
                }));
            });
        }

        // Add AntiForgery Token
        var token = $('#antiForgeryPlaceholder input[name="__RequestVerificationToken"]').val();
        if (token) {
            form.append($('<input>', {
                'type': 'hidden',
                'name': '__RequestVerificationToken',
                'value': token
            }));
        }

        $('body').append(form);
        form.submit();
    }

    // Bulk activate
    $(document).on('click', '#bulkActivate', function () {
        var ids = getSelectedIds();
        if (ids.length === 0) {
            alert('Please select at least one room.');
            return;
        }
        if (confirm('Are you sure you want to activate ' + ids.length + ' room(s)?')) {
            submitBulkAction('/Room/BatchInactive', { 'makeActive': true });
        }
    });

    // Bulk deactivate
    $(document).on('click', '#bulkDeactivate', function () {
        var ids = getSelectedIds();
        if (ids.length === 0) {
            alert('Please select at least one room.');
            return;
        }
        if (confirm('Are you sure you want to deactivate ' + ids.length + ' room(s)?')) {
            submitBulkAction('/Room/BatchInactive', { 'makeActive': false });
        }
    });

    // Bulk update type
    $(document).on('click', '#bulkUpdateType', function () {
        var ids = getSelectedIds();
        var newTypeId = $('#newTypeSelect').val();

        if (ids.length === 0) {
            alert('Please select at least one room.');
            return;
        }
        if (!newTypeId) {
            alert('Please select a new room type.');
            return;
        }

        var typeName = $('#newTypeSelect option:selected').text();
        if (confirm('Are you sure you want to update ' + ids.length + ' room(s) to type "' + typeName + '"?')) {
            submitBulkAction('/Room/BatchUpdate', { 'newTypeId': newTypeId });
        }
    });

    // Initialize on first load
    function initOnce() {
        updateSelectAllState();
        toggleBulkActions();
    }
    initOnce();

    // Re-initialize after Ajax page reload
    $(document).on('ajaxSuccess', function () {
        initOnce();
    });
});