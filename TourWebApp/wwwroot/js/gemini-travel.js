
(() => {
    const naturalInput = document.getElementById("htAiNaturalInput");
    const naturalSubmit = document.getElementById("htAiNaturalSubmit");
    const message = document.getElementById("htAiNaturalMessage");
    const naturalSection = document.getElementById("htAiNaturalLanguage");

    const form = document.getElementById("htAiRecommendationForm");
    const loading = document.getElementById("htAiLoading");
    const content = document.getElementById("htAiResultContent");
    const submitButton = document.getElementById("htAiSubmit");

    const destinationSelect = document.getElementById("IdDiemDen");
    const budgetInput = document.getElementById("NganSachToiDa");
    const daysInput = document.getElementById("SoNgayToiDa");

    const confirmPanel = document.getElementById("htAiConfirm");
    const confirmDestination = document.getElementById("htAiConfirmDestination");
    const confirmInterests = document.getElementById("htAiConfirmInterests");
    const confirmBudget = document.getElementById("htAiConfirmBudget");
    const confirmDays = document.getElementById("htAiConfirmDays");

    const confirmSearch = document.getElementById("htAiConfirmSearch");
    const confirmEdit = document.getElementById("htAiConfirmEdit");

    if (
        !naturalInput ||
        !naturalSubmit ||
        !message ||
        !form ||
        !destinationSelect ||
        !budgetInput ||
        !daysInput
    ) {
        return;
    }

    const interestInputs = Array.from(
        form.querySelectorAll('input[name="IdSoThich"]')
    );

    const token = document.querySelector(
        '#htAiNaturalLanguage input[name="__RequestVerificationToken"]'
    );

    const originalSubmitHtml = naturalSubmit.innerHTML;

    let activeController = null;
    let requestNumber = 0;
    let isAnalyzing = false;

    const normalize = value => {
        return String(value ?? "")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/đ/g, "d")
            .replace(/Đ/g, "D")
            .trim()
            .toLowerCase()
            .replace(/\s+/g, " ");
    };

    const getInterestName = input => {
        const label = input.closest("label");

        if (!label) {
            return "";
        }

        const name = label.querySelector("span");

        return (name?.textContent || label.textContent || "").trim();
    };

    const destinations = Array.from(destinationSelect.options)
        .filter(option => option.value !== "")
        .map(option => option.textContent.trim());

    const interests = interestInputs
        .map(getInterestName)
        .filter(Boolean);

    const formatMoney = value => {
        const number = Number(value);

        if (!Number.isFinite(number) || number <= 0) {
            return "Không giới hạn";
        }

        return new Intl.NumberFormat("vi-VN", {
            style: "currency",
            currency: "VND",
            maximumFractionDigits: 0
        }).format(number);
    };

    const setMessage = (text, isError = false) => {
        message.hidden = false;
        message.textContent = text;
        message.classList.toggle("is-error", isError);
    };

    const hideMessage = () => {
        message.hidden = true;
        message.textContent = "";
        message.classList.remove("is-error");
    };

    const setAnalyzing = value => {
        isAnalyzing = value;
        naturalSubmit.disabled = value;

        naturalSubmit.innerHTML = value
            ? '<i class="fa-solid fa-spinner fa-spin"></i> Tripi đang phân tích...'
            : originalSubmitHtml;
    };

    const getSelectedDestination = () => {
        if (!destinationSelect.value) {
            return "Chưa lựa chọn";
        }

        const selectedOption =
            destinationSelect.options[destinationSelect.selectedIndex];

        return selectedOption?.textContent.trim() || "Chưa lựa chọn";
    };

    const getSelectedInterests = () => {
        return interestInputs
            .filter(input => input.checked)
            .map(getInterestName)
            .filter(Boolean);
    };

    const hasAnyCriteria = () => {
        return (
            destinationSelect.value !== "" ||
            interestInputs.some(input => input.checked) ||
            budgetInput.value !== "" ||
            daysInput.value !== ""
        );
    };

    const updateConfirmation = () => {
        if (!confirmPanel) {
            return;
        }

        const selectedInterests = getSelectedInterests();
        const days = Number(daysInput.value);

        if (confirmDestination) {
            confirmDestination.textContent = getSelectedDestination();
        }

        if (confirmInterests) {
            confirmInterests.textContent = selectedInterests.length
                ? selectedInterests.join(", ")
                : "Chưa lựa chọn";
        }

        if (confirmBudget) {
            confirmBudget.textContent = formatMoney(budgetInput.value);
        }

        if (confirmDays) {
            confirmDays.textContent =
                daysInput.value &&
                Number.isInteger(days) &&
                days > 0
                    ? `Tối đa ${days} ngày`
                    : "Không giới hạn";
        }

        if (confirmSearch) {
            confirmSearch.disabled = !hasAnyCriteria();
        }
    };

    const showConfirmation = () => {
        if (!confirmPanel) {
            return;
        }

        updateConfirmation();
        confirmPanel.hidden = false;

        requestAnimationFrame(() => {
            confirmPanel.scrollIntoView({
                behavior: "smooth",
                block: "nearest"
            });
        });
    };

    const hideConfirmation = () => {
        if (confirmPanel) {
            confirmPanel.hidden = true;
        }
    };

    const cancelCurrentAnalysis = () => {
        requestNumber += 1;

        if (activeController) {
            activeController.abort();
            activeController = null;
        }

        if (isAnalyzing) {
            setAnalyzing(false);
        }
    };

    const applyDestination = destinationName => {
        const normalizedName = normalize(destinationName);

        destinationSelect.value = "";

        if (!normalizedName) {
            return;
        }

        const matchedOption = Array.from(destinationSelect.options)
            .find(option => {
                return (
                    option.value !== "" &&
                    normalize(option.textContent) === normalizedName
                );
            });

        if (matchedOption) {
            destinationSelect.value = matchedOption.value;
        }
    };

    const applyInterests = selectedNames => {
        const selected = new Set(
            (Array.isArray(selectedNames) ? selectedNames : [])
                .map(normalize)
        );

        interestInputs.forEach(input => {
            input.checked = selected.has(normalize(getInterestName(input)));
        });
    };

    const applyGeminiResult = data => {
        applyDestination(data.diemDen);
        applyInterests(data.soThich);

        const budget = Number(data.nganSach);
        const days = Number(data.soNgay);

        budgetInput.value =
            data.nganSach !== null &&
            data.nganSach !== undefined &&
            Number.isFinite(budget) &&
            budget > 0
                ? Math.floor(budget)
                : "";

        daysInput.value =
            data.soNgay !== null &&
            data.soNgay !== undefined &&
            Number.isInteger(days) &&
            days > 0
                ? days
                : "";

        updateConfirmation();
    };

    const analyzeWithGemini = async () => {
        const text = naturalInput.value.trim();

        if (!text) {
            setMessage("Bạn hãy nhập mong muốn du lịch trước nhé.", true);
            naturalInput.focus();
            return;
        }

        if (text.length > 1000) {
            setMessage("Yêu cầu không được vượt quá 1000 ký tự.", true);
            return;
        }

        cancelCurrentAnalysis();

        const currentRequest = requestNumber;
        const controller = new AbortController();

        activeController = controller;

        hideMessage();
        hideConfirmation();
        setAnalyzing(true);

        try {
            const response = await fetch("/api/gemini/phan-tich", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token?.value || ""
                },
                body: JSON.stringify({
                    noiDung: text,
                    diemDens: destinations,
                    soThichs: interests
                }),
                signal: controller.signal
            });

            const data = await response.json();

            if (currentRequest !== requestNumber) {
                return;
            }

            if (!response.ok) {
                throw new Error(
                    data?.message ||
                    "Gemini chưa xử lý được yêu cầu."
                );
            }

            applyGeminiResult(data);

            if (!hasAnyCriteria()) {
                setMessage(
                    "Tripi chưa nhận diện được tiêu chí phù hợp. " +
                    "Bạn thử mô tả rõ điểm đến, sở thích, " +
                    "ngân sách hoặc số ngày nhé.",
                    true
                );
                return;
            }

            setMessage(
                "Tripi đã phân tích xong! " +
                "Bạn hãy kiểm tra các tiêu chí vừa nhận diện."
            );

            showConfirmation();
        } catch (error) {
            if (currentRequest !== requestNumber) {
                return;
            }

            if (error.name === "AbortError") {
                return;
            }

            setMessage(
                error.message ||
                "Không thể kết nối Gemini lúc này.",
                true
            );
        } finally {
            if (currentRequest === requestNumber) {
                activeController = null;
                setAnalyzing(false);
            }
        }
    };

    naturalSubmit.addEventListener("click", analyzeWithGemini);

    document.querySelectorAll(".ht-ai-example").forEach(button => {
        button.addEventListener("click", () => {
            cancelCurrentAnalysis();
            hideConfirmation();
            hideMessage();

            naturalInput.value = button.dataset.example || "";
            naturalInput.focus();
        });
    });

    naturalInput.addEventListener("input", () => {
        if (isAnalyzing) {
            cancelCurrentAnalysis();
        }

        hideConfirmation();
        hideMessage();
    });

    const updateOnManualChange = () => {
        if (confirmPanel && !confirmPanel.hidden) {
            updateConfirmation();
        }
    };

    destinationSelect.addEventListener("change", updateOnManualChange);
    budgetInput.addEventListener("input", updateOnManualChange);
    daysInput.addEventListener("input", updateOnManualChange);

    interestInputs.forEach(input => {
        input.addEventListener("change", updateOnManualChange);
    });

    const submitMatchingScore = () => {
        if (!form.reportValidity()) {
            setMessage(
                "Bạn hãy kiểm tra lại ngân sách và số ngày trước khi tìm tour.",
                true
            );

            form.scrollIntoView({
                behavior: "smooth",
                block: "start"
            });

            return;
        }

        if (!hasAnyCriteria()) {
            setMessage(
                "Bạn hãy chọn ít nhất một tiêu chí trước khi tìm tour nhé.",
                true
            );
            return;
        }

        if (submitButton) {
            submitButton.click();
            return;
        }

        const parameters = new URLSearchParams(
            new FormData(form)
        );

        parameters.set("tim", "1");

        const url = new URL(
            form.getAttribute("action") || "/goi-y-tour",
            window.location.origin
        );

        url.search = parameters.toString();

        window.location.assign(url.toString());
    };

    confirmSearch?.addEventListener("click", submitMatchingScore);

    confirmEdit?.addEventListener("click", () => {
        form.scrollIntoView({
            behavior: "smooth",
            block: "start"
        });

        window.setTimeout(() => {
            destinationSelect.focus({
                preventScroll: true
            });
        }, 350);
    });

    if (loading && content && submitButton) {
        const originalFormSubmitHtml = submitButton.innerHTML;

        form.addEventListener("submit", event => {
            if (event.defaultPrevented || !form.checkValidity()) {
                return;
            }

            loading.hidden = false;
            content.hidden = true;

            submitButton.innerHTML =
                '<i class="fa-solid fa-spinner fa-spin"></i> Đang tìm tour...';

            // Không khóa nút submit tại đây vì nó chứa name="tim".
        });

        window.addEventListener("pageshow", () => {
            loading.hidden = true;
            content.hidden = false;
            submitButton.disabled = false;
            submitButton.innerHTML = originalFormSubmitHtml;
        });
    }

    if (naturalSection) {
        naturalSection.addEventListener("keydown", event => {
            if (
                event.target === naturalInput &&
                event.key === "Enter" &&
                (event.ctrlKey || event.metaKey)
            ) {
                event.preventDefault();

                if (!isAnalyzing) {
                    analyzeWithGemini();
                }
            }
        });
    }
})();
