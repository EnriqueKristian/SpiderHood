function openTab(tabName) {
    let i;
    let tabContent;

    tabContent = document.getElementsByClassName("tab-content");

    for (i = 0; i < tabContent.length; i++) {
        tabContent[i].style.display = "none";
    }

    document.getElementById(tabName).style.display = "flex";
}

try {
    let designLinkEl = document.getElementById("DesignLink");
    designLinkEl.addEventListener("click", function () { openTab("Design") }, false);

    let progLinkEl = document.getElementById("ProgLink");
    progLinkEl.addEventListener("click", function () { openTab("Programming") }, false);

    let musicLinkEl = document.getElementById("SupportLink");
    musicLinkEl.addEventListener("click", function () { openTab("Support") }, false);
} catch (e) { }


function toggleShow() {
	const labels = document.querySelectorAll(".accordion-item__label");
	const tabs = document.querySelectorAll(".accordion-tab");

	const target = this;
	const item = target.classList.contains("accordion-tab")
		? target
		: target.parentElement;
	const group = item.dataset.actabGroup;
	const id = item.dataset.actabId;

	tabs.forEach(function (tab) {
		if (tab.dataset.actabGroup === group) {
			if (tab.dataset.actabId === id) {
				tab.classList.add("accordion-active");
			} else {
				tab.classList.remove("accordion-active");
			}
		}
	});

	labels.forEach(function (label) {
		const tabItem = label.parentElement;

		if (tabItem.dataset.actabGroup === group) {
			if (tabItem.dataset.actabId === id) {
				tabItem.classList.add("accordion-active");
			} else {
				tabItem.classList.remove("accordion-active");
			}
		}
	});
}
try {
	const labels = document.querySelectorAll(".accordion-item__label");
	const tabs = document.querySelectorAll(".accordion-tab");

	labels.forEach(function (label) {
		label.addEventListener("click", toggleShow);
	});

	tabs.forEach(function (tab) {
		tab.addEventListener("click", toggleShow);
	});
} catch (e) { }