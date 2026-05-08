(function (window) {
    const instances = new Map();

    function pad(value) {
        return String(value).padStart(2, "0");
    }

    function toLocalIso(date, allDay) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        const year = date.getFullYear();
        const month = pad(date.getMonth() + 1);
        const day = pad(date.getDate());

        if (allDay) {
            return `${year}-${month}-${day}`;
        }

        const hours = pad(date.getHours());
        const minutes = pad(date.getMinutes());
        const seconds = pad(date.getSeconds());

        return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;
    }

    function normalizeView(viewName) {
        // Month view only (week/day removed from headerToolbar).
        return "dayGridMonth";
    }

    function mapEvents(events) {
        if (!Array.isArray(events)) {
            return [];
        }

        return events.map((event) => ({
            id: event.id,
            title: event.title,
            start: event.start,
            end: event.end,
            allDay: event.allDay,
            classNames: event.classNames || [],
            extendedProps: event.extendedProps || {}
        }));
    }

    function setCallbacks(calendar, dotNetRef) {
        calendar.setOption("datesSet", function (info) {
            if (!dotNetRef) {
                return;
            }

            dotNetRef.invokeMethodAsync(
                "HandleCalendarDatesSet",
                toLocalIso(info.start, false),
                toLocalIso(info.end, false),
                toLocalIso(info.view.calendar.getDate(), false),
                info.view.type
            ).catch(console.error);
        });

        calendar.setOption("dateClick", function (info) {
            if (!dotNetRef) {
                return;
            }

            dotNetRef.invokeMethodAsync(
                "HandleCalendarDateClick",
                toLocalIso(info.date, info.allDay)
            ).catch(console.error);
        });

        calendar.setOption("eventClick", function (info) {
            if (!dotNetRef) {
                return;
            }

            const calendarEventId = info.event.extendedProps && info.event.extendedProps.calendarEventId
                ? info.event.extendedProps.calendarEventId
                : info.event.id;

            if (!calendarEventId) {
                return;
            }

            dotNetRef.invokeMethodAsync("HandleCalendarEventClick", calendarEventId).catch(console.error);
        });
    }

    function createCalendar(element, options, dotNetRef) {
        const calendar = new FullCalendar.Calendar(element, {
            initialView: normalizeView(options.initialView),
            initialDate: options.initialDate,
            locale: options.locale || "vi",
            firstDay: 1,
            height: "auto",
            stickyHeaderDates: true,
            expandRows: true,
            dayMaxEventRows: true,
            nowIndicator: true,
            headerToolbar: {
                left: "prev,next today",
                center: "title",
                right: ""
            },
            buttonText: options.buttonText || {},
            eventTimeFormat: {
                hour: "2-digit",
                minute: "2-digit",
                meridiem: false
            },
            slotLabelFormat: {
                hour: "2-digit",
                minute: "2-digit",
                meridiem: false
            },
            eventDidMount: function (info) {
                const description = info.event.extendedProps && info.event.extendedProps.description
                    ? info.event.extendedProps.description
                    : "";

                info.el.title = description
                    ? `${info.event.title}\n${description}`
                    : info.event.title;
            }
        });

        setCallbacks(calendar, dotNetRef);
        mapEvents(options.events).forEach((event) => calendar.addEvent(event));

        return calendar;
    }

    function render(elementId, options, dotNetRef) {
        if (!window.FullCalendar) {
            console.error("FullCalendar is not loaded.");
            return;
        }

        const element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        const normalizedView = normalizeView(options && options.initialView);
        const mappedEvents = mapEvents(options && options.events);
        const instance = instances.get(elementId);

        if (!instance) {
            const calendar = createCalendar(element, options || {}, dotNetRef);
            instances.set(elementId, { calendar: calendar });
            calendar.render();
            return;
        }

        const calendar = instance.calendar;

        setCallbacks(calendar, dotNetRef);
        calendar.setOption("locale", (options && options.locale) || "vi");
        calendar.setOption("buttonText", (options && options.buttonText) || {});

        calendar.batchRendering(function () {
            if (calendar.view.type !== normalizedView) {
                calendar.changeView(normalizedView);
            }

            if (options && options.initialDate) {
                calendar.gotoDate(options.initialDate);
            }

            calendar.removeAllEvents();
            mappedEvents.forEach((event) => calendar.addEvent(event));
        });
    }

    function destroy(elementId) {
        const instance = instances.get(elementId);
        if (!instance) {
            return;
        }

        instance.calendar.destroy();
        instances.delete(elementId);
    }

    window.hcCalendarEvents = {
        render: render,
        destroy: destroy
    };
})(window);



