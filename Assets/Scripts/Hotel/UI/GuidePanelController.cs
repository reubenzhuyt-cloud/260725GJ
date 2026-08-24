using System;
using System.Collections.Generic;
using Hotel.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuidePanelController : MonoBehaviour
{
    [System.Serializable]
    public class GuidePageData
    {
        public string image;
        public string content;
        public string title;
    }

    [System.Serializable]
    public class GuideData
    {
        public string name;
        public string title;
        public int imagecount;
        public List<GuidePageData> pages = new List<GuidePageData>();
        public List<GuidePageData> items = new List<GuidePageData>();
        public List<GuidePageData> list = new List<GuidePageData>();

        [NonSerialized] public string resourceFolderPath = string.Empty;

        public List<GuidePageData> GetValidPages()
        {
            if (pages != null && pages.Count > 0) return pages;
            if (items != null && items.Count > 0) return items;
            if (list != null && list.Count > 0) return list;
            return new List<GuidePageData>();
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(name)) return name;
            if (!string.IsNullOrEmpty(title)) return title;
            return "Guide";
        }
    }

    [Header("UI References - Container")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameObject guidePanelRoot;

    [Header("UI References - Left Panel")]
    [SerializeField] private Transform leftContentContainer;
    [SerializeField] private GameObject contentPanelPrefab;

    [Header("UI References - Right Panel")]
    [SerializeField] private Image picturePanel;
    [SerializeField] private Image guideImage;
    [SerializeField] private Image pictureImage;
    [SerializeField] private TextMeshProUGUI describeText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI pageIndicatorText;
    [SerializeField] private GameObject dot;
    [SerializeField] private Transform dotContainer;
    [SerializeField] private GameObject dotPrefab;
    [SerializeField] private Color activeDotColor = Color.black;
    [SerializeField] private Color inactiveDotColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private Button prevPageBtn;
    [SerializeField] private Button nextPageBtn;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button closeBtn;
    [SerializeField] private Button exitButton;

    private readonly List<GuideData> _loadedGuides = new List<GuideData>();
    private readonly List<GameObject> _spawnedMenuItems = new List<GameObject>();
    private readonly List<Image> _spawnedDots = new List<Image>();
    private int _selectedGuideIndex = -1;
    private int _currentPageIndex = 0;

    public int CurrentPageIndex => _currentPageIndex;
    public int SelectedGuideIndex => _selectedGuideIndex;
    public int PageCount
    {
        get
        {
            if (_selectedGuideIndex >= 0 && _selectedGuideIndex < _loadedGuides.Count)
            {
                var pages = _loadedGuides[_selectedGuideIndex].GetValidPages();
                return pages != null ? pages.Count : 0;
            }
            return 0;
        }
    }
    public bool IsOpen => guidePanelRoot != null ? guidePanelRoot.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        AutoBindComponents();
        RegisterEvents();
    }

    private void Start()
    {
        LoadAllGuides();
    }

    private void OnDestroy()
    {
        UnregisterEvents();
        CleanupDots();
    }

    private void CleanupDots()
    {
        for (int i = 0; i < _spawnedDots.Count; i++)
        {
            if (_spawnedDots[i] != null && _spawnedDots[i].gameObject != null)
            {
                Destroy(_spawnedDots[i].gameObject);
            }
        }
        _spawnedDots.Clear();
    }

    private void AutoBindComponents()
    {
        if (guidePanelRoot == null)
            guidePanelRoot = gameObject;

        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        if (leftContentContainer == null)
        {
            Transform contentTransform = transform.Find("LeftPanel/Scroll View/Viewport/Content")
                ?? transform.Find("LeftPanel/ScrollView/Viewport/Content")
                ?? transform.Find("LeftPanel/Content");
            if (contentTransform != null)
                leftContentContainer = contentTransform;
        }

        if (contentPanelPrefab == null && leftContentContainer != null)
        {
            Transform template = leftContentContainer.Find("ContentPanel");
            if (template != null)
            {
                contentPanelPrefab = template.gameObject;
                contentPanelPrefab.SetActive(false);
            }
        }

        if (picturePanel == null && guideImage == null && pictureImage == null)
        {
            Transform picTransform = transform.Find("RightPanel/PicturePanel")
                ?? transform.Find("RightPanel/PictureImage")
                ?? transform.Find("RightPanel/GuideImage")
                ?? transform.Find("RightPanel/Image");

            if (picTransform != null)
            {
                picturePanel = picTransform.GetComponent<Image>();
                pictureImage = picturePanel;
                guideImage = picturePanel;
            }
        }
        else
        {
            if (picturePanel == null) picturePanel = pictureImage != null ? pictureImage : guideImage;
            if (pictureImage == null) pictureImage = picturePanel != null ? picturePanel : guideImage;
            if (guideImage == null) guideImage = picturePanel != null ? picturePanel : pictureImage;
        }

        if (picturePanel != null) picturePanel.preserveAspect = true;
        if (pictureImage != null) pictureImage.preserveAspect = true;
        if (guideImage != null) guideImage.preserveAspect = true;

        if (describeText == null && contentText == null)
        {
            Transform descTransform = transform.Find("RightPanel/DescribeText")
                ?? transform.Find("RightPanel/ContentText")
                ?? transform.Find("RightPanel/Describe")
                ?? transform.Find("RightPanel/Text");

            if (descTransform != null)
            {
                describeText = descTransform.GetComponent<TextMeshProUGUI>();
                contentText = describeText;
            }
        }
        else
        {
            if (describeText == null) describeText = contentText;
            if (contentText == null) contentText = describeText;
        }

        if (dotPrefab == null && dot != null)
        {
            dotPrefab = dot;
        }
        else if (dotPrefab == null && dot == null)
        {
            Transform dotTransform = transform.Find("RightPanel/ControlPanel/Dot")
                ?? transform.Find("RightPanel/Dot")
                ?? transform.Find("Dot");
            if (dotTransform != null)
            {
                dotPrefab = dotTransform.gameObject;
                dot = dotPrefab;
            }
        }
        else if (dot == null && dotPrefab != null)
        {
            dot = dotPrefab;
        }

        if (dotContainer == null)
        {
            Transform container = transform.Find("RightPanel/ControlPanel")
                ?? transform.Find("ControlPanel");
            if (container != null)
                dotContainer = container;
            else if (dotPrefab != null)
                dotContainer = dotPrefab.transform.parent;
        }

        if (dotPrefab != null)
            dotPrefab.SetActive(false);

        if (prevPageBtn == null && previousButton == null && leftButton == null)
        {
            Transform btn = transform.Find("RightPanel/ControlPanel/LeftButton")
                ?? transform.Find("RightPanel/InfoContent/LeftButton")
                ?? transform.Find("RightPanel/LeftButton")
                ?? transform.Find("RightPanel/PrevPageBtn")
                ?? transform.Find("RightPanel/PreviousButton");

            if (btn != null)
            {
                prevPageBtn = btn.GetComponent<Button>();
                leftButton = prevPageBtn;
                previousButton = prevPageBtn;
            }
        }
        else
        {
            if (prevPageBtn == null) prevPageBtn = leftButton != null ? leftButton : previousButton;
            if (leftButton == null) leftButton = prevPageBtn != null ? prevPageBtn : previousButton;
            if (previousButton == null) previousButton = prevPageBtn != null ? prevPageBtn : leftButton;
        }

        if (nextPageBtn == null && nextButton == null && rightButton == null)
        {
            Transform btn = transform.Find("RightPanel/ControlPanel/RightButton")
                ?? transform.Find("RightPanel/InfoContent/RightButton")
                ?? transform.Find("RightPanel/RightButton")
                ?? transform.Find("RightPanel/NextPageBtn")
                ?? transform.Find("RightPanel/NextButton");

            if (btn != null)
            {
                nextPageBtn = btn.GetComponent<Button>();
                rightButton = nextPageBtn;
                nextButton = nextPageBtn;
            }
        }
        else
        {
            if (nextPageBtn == null) nextPageBtn = rightButton != null ? rightButton : nextButton;
            if (rightButton == null) rightButton = nextPageBtn != null ? nextPageBtn : nextButton;
            if (nextButton == null) nextButton = nextPageBtn != null ? nextPageBtn : rightButton;
        }

        if (closeBtn == null && exitButton == null)
        {
            Transform btn = transform.Find("ExitButton")
                ?? transform.Find("CloseButton")
                ?? transform.Find("CloseBtn")
                ?? transform.Find("RightPanel/CloseBtn")
                ?? transform.Find("RightPanel/ExitButton");

            if (btn != null)
            {
                closeBtn = btn.GetComponent<Button>();
                exitButton = closeBtn;
            }
        }
        else
        {
            if (closeBtn == null) closeBtn = exitButton;
            if (exitButton == null) exitButton = closeBtn;
        }
    }

    private void RegisterEvents()
    {
        if (closeBtn != null)
            closeBtn.onClick.AddListener(OnCloseButtonClicked);
        if (exitButton != null && exitButton != closeBtn)
            exitButton.onClick.AddListener(OnCloseButtonClicked);

        if (prevPageBtn != null)
            prevPageBtn.onClick.AddListener(OnPreviousButtonClicked);
        if (previousButton != null && previousButton != prevPageBtn)
            previousButton.onClick.AddListener(OnPreviousButtonClicked);
        if (leftButton != null && leftButton != prevPageBtn && leftButton != previousButton)
            leftButton.onClick.AddListener(OnPreviousButtonClicked);

        if (nextPageBtn != null)
            nextPageBtn.onClick.AddListener(OnNextButtonClicked);
        if (nextButton != null && nextButton != nextPageBtn)
            nextButton.onClick.AddListener(OnNextButtonClicked);
        if (rightButton != null && rightButton != nextPageBtn && rightButton != nextButton)
            rightButton.onClick.AddListener(OnNextButtonClicked);
    }

    private void UnregisterEvents()
    {
        if (closeBtn != null)
            closeBtn.onClick.RemoveListener(OnCloseButtonClicked);
        if (exitButton != null && exitButton != closeBtn)
            exitButton.onClick.RemoveListener(OnCloseButtonClicked);

        if (prevPageBtn != null)
            prevPageBtn.onClick.RemoveListener(OnPreviousButtonClicked);
        if (previousButton != null && previousButton != prevPageBtn)
            previousButton.onClick.RemoveListener(OnPreviousButtonClicked);
        if (leftButton != null && leftButton != prevPageBtn && leftButton != previousButton)
            leftButton.onClick.RemoveListener(OnPreviousButtonClicked);

        if (nextPageBtn != null)
            nextPageBtn.onClick.RemoveListener(OnNextButtonClicked);
        if (nextButton != null && nextButton != nextPageBtn)
            nextButton.onClick.RemoveListener(OnNextButtonClicked);
        if (rightButton != null && rightButton != nextPageBtn && rightButton != nextButton)
            rightButton.onClick.RemoveListener(OnNextButtonClicked);
    }

    public void LoadAllGuides()
    {
        _loadedGuides.Clear();

        TextAsset[] guideAssets = Resources.LoadAll<TextAsset>("Guide");
        if (guideAssets != null)
        {
            Array.Sort(guideAssets, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var asset in guideAssets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.text))
                    continue;

                try
                {
                    GuideData guide = JsonUtility.FromJson<GuideData>(asset.text);
                    if (guide != null)
                    {
                        guide.resourceFolderPath = $"Guide/{asset.name}";
                        _loadedGuides.Add(guide);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GuidePanelController] Failed to parse guide json {asset.name}: {ex.Message}");
                }
            }
        }

        RebuildLeftMenu();
    }

    private void RebuildLeftMenu()
    {
        foreach (var item in _spawnedMenuItems)
        {
            if (item != null)
                Destroy(item);
        }
        _spawnedMenuItems.Clear();

        if (leftContentContainer == null || contentPanelPrefab == null)
            return;

        for (int i = 0; i < _loadedGuides.Count; i++)
        {
            int guideIndex = i;
            GuideData guide = _loadedGuides[guideIndex];

            GameObject itemObj = Instantiate(contentPanelPrefab, leftContentContainer);
            itemObj.name = $"GuideItem_{guideIndex}";
            itemObj.SetActive(true);

            TextMeshProUGUI tmpText = itemObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                tmpText.text = guide.GetDisplayName();
            }
            else
            {
                Text text = itemObj.GetComponentInChildren<Text>(true);
                if (text != null)
                    text.text = guide.GetDisplayName();
            }

            Button btn = itemObj.GetComponent<Button>();
            if (btn == null)
                btn = itemObj.AddComponent<Button>();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectGuide(guideIndex));

            _spawnedMenuItems.Add(itemObj);
        }
    }

    public void SelectGuide(int index)
    {
        if (index < 0 || index >= _loadedGuides.Count)
            return;

        _selectedGuideIndex = index;
        _currentPageIndex = 0;
        RefreshUI();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);
    }

    public void Open()
    {
        if (uiManager != null && !uiManager.CanOpenButtonPanel())
            return;

        if (guidePanelRoot != null)
            guidePanelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (_loadedGuides.Count == 0)
            LoadAllGuides();

        if (_loadedGuides.Count > 0)
        {
            if (_selectedGuideIndex < 0 || _selectedGuideIndex >= _loadedGuides.Count)
                _selectedGuideIndex = 0;
            _currentPageIndex = 0;
        }

        RefreshUI();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelOpen);
    }

    public void Close()
    {
        if (guidePanelRoot != null)
            guidePanelRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelClose);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void NextPage()
    {
        if (_selectedGuideIndex < 0 || _selectedGuideIndex >= _loadedGuides.Count)
            return;

        var pages = _loadedGuides[_selectedGuideIndex].GetValidPages();
        if (pages == null || pages.Count == 0)
            return;

        if (_currentPageIndex < pages.Count - 1)
        {
            _currentPageIndex++;
            RefreshUI();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUISound(UISoundType.Click);
        }
    }

    public void PreviousPage()
    {
        if (_selectedGuideIndex < 0 || _selectedGuideIndex >= _loadedGuides.Count)
            return;

        if (_currentPageIndex > 0)
        {
            _currentPageIndex--;
            RefreshUI();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUISound(UISoundType.Click);
        }
    }

    public void SetPage(int pageIndex)
    {
        if (_selectedGuideIndex < 0 || _selectedGuideIndex >= _loadedGuides.Count)
            return;

        var pages = _loadedGuides[_selectedGuideIndex].GetValidPages();
        if (pages == null || pages.Count == 0)
            return;

        _currentPageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
        RefreshUI();
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn != null)
            btn.interactable = interactable;
    }

    private void RefreshUI()
    {
        if (_selectedGuideIndex < 0 || _selectedGuideIndex >= _loadedGuides.Count)
        {
            SetButtonInteractable(prevPageBtn, false);
            SetButtonInteractable(previousButton, false);
            SetButtonInteractable(leftButton, false);

            SetButtonInteractable(nextPageBtn, false);
            SetButtonInteractable(nextButton, false);
            SetButtonInteractable(rightButton, false);

            if (pageIndicatorText != null) pageIndicatorText.text = string.Empty;
            if (describeText != null) describeText.text = string.Empty;
            if (contentText != null && contentText != describeText) contentText.text = string.Empty;
            if (titleText != null) titleText.text = string.Empty;
            if (picturePanel != null) picturePanel.gameObject.SetActive(false);
            if (guideImage != null && guideImage != picturePanel) guideImage.gameObject.SetActive(false);

            UpdateDots(0, 0);
            return;
        }

        GuideData guide = _loadedGuides[_selectedGuideIndex];
        var pages = guide.GetValidPages();

        if (titleText != null)
            titleText.text = guide.GetDisplayName();

        if (pages == null || pages.Count == 0)
        {
            if (describeText != null) describeText.text = string.Empty;
            if (contentText != null && contentText != describeText) contentText.text = string.Empty;
            if (picturePanel != null) picturePanel.gameObject.SetActive(false);
            if (guideImage != null && guideImage != picturePanel) guideImage.gameObject.SetActive(false);

            SetButtonInteractable(prevPageBtn, false);
            SetButtonInteractable(previousButton, false);
            SetButtonInteractable(leftButton, false);

            SetButtonInteractable(nextPageBtn, false);
            SetButtonInteractable(nextButton, false);
            SetButtonInteractable(rightButton, false);

            if (pageIndicatorText != null) pageIndicatorText.text = string.Empty;
            UpdateDots(0, 0);
            return;
        }

        _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, pages.Count - 1);
        GuidePageData page = pages[_currentPageIndex];

        if (describeText != null)
            describeText.text = page.content ?? string.Empty;
        if (contentText != null && contentText != describeText)
            contentText.text = page.content ?? string.Empty;

        Sprite sprite = LoadPageSprite(guide, page);
        SetupGuideImage(picturePanel, sprite);
        if (guideImage != null && guideImage != picturePanel)
        {
            SetupGuideImage(guideImage, sprite);
        }
        if (pictureImage != null && pictureImage != picturePanel && pictureImage != guideImage)
        {
            SetupGuideImage(pictureImage, sprite);
        }

        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{_currentPageIndex + 1} / {pages.Count}";

        UpdateDots(pages.Count, _currentPageIndex);

        bool hasPrev = _currentPageIndex > 0;
        bool hasNext = _currentPageIndex < pages.Count - 1;

        SetButtonInteractable(prevPageBtn, hasPrev);
        SetButtonInteractable(previousButton, hasPrev);
        SetButtonInteractable(leftButton, hasPrev);

        SetButtonInteractable(nextPageBtn, hasNext);
        SetButtonInteractable(nextButton, hasNext);
        SetButtonInteractable(rightButton, hasNext);
    }

    private void SetupGuideImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
            return;

        if (sprite != null)
        {
            targetImage.sprite = sprite;
            targetImage.type = Image.Type.Simple;
            targetImage.preserveAspect = true;

            AspectRatioFitter fitter = targetImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = targetImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            if (sprite.rect.height > 0)
            {
                fitter.aspectRatio = (float)sprite.rect.width / sprite.rect.height;
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }

            targetImage.gameObject.SetActive(true);
        }
        else
        {
            targetImage.gameObject.SetActive(false);
        }
    }

    private void UpdateDots(int totalPages, int activeIndex)
    {
        if (dotPrefab == null && dot != null)
            dotPrefab = dot;

        if (dotPrefab != null && dotPrefab.activeSelf)
            dotPrefab.SetActive(false);

        if (totalPages <= 0 || dotPrefab == null)
        {
            for (int i = 0; i < _spawnedDots.Count; i++)
            {
                if (_spawnedDots[i] != null)
                    _spawnedDots[i].gameObject.SetActive(false);
            }
            return;
        }

        Transform container = dotContainer != null ? dotContainer : dotPrefab.transform.parent;
        if (container == null)
            return;

        while (_spawnedDots.Count < totalPages)
        {
            GameObject dotObj = Instantiate(dotPrefab, container);
            dotObj.name = $"Dot_{_spawnedDots.Count}";
            dotObj.SetActive(true);

            Transform rightBtnTransform = rightButton != null ? rightButton.transform : (nextPageBtn != null ? nextPageBtn.transform : null);
            Transform leftBtnTransform = leftButton != null ? leftButton.transform : (prevPageBtn != null ? prevPageBtn.transform : null);

            if (rightBtnTransform != null && rightBtnTransform.parent == container)
            {
                dotObj.transform.SetSiblingIndex(rightBtnTransform.GetSiblingIndex());
            }
            else if (leftBtnTransform != null && leftBtnTransform.parent == container)
            {
                dotObj.transform.SetSiblingIndex(leftBtnTransform.GetSiblingIndex() + 1);
            }

            Image img = dotObj.GetComponent<Image>();
            _spawnedDots.Add(img);
        }

        for (int i = 0; i < _spawnedDots.Count; i++)
        {
            Image dotImg = _spawnedDots[i];
            if (dotImg == null) continue;

            if (i < totalPages)
            {
                dotImg.gameObject.SetActive(true);
                dotImg.color = (i == activeIndex) ? activeDotColor : inactiveDotColor;
            }
            else
            {
                dotImg.gameObject.SetActive(false);
            }
        }
    }

    private Sprite LoadPageSprite(GuideData guide, GuidePageData page)
    {
        Sprite sprite = null;
        string imageName = page != null ? page.image : null;

        if (!string.IsNullOrEmpty(imageName))
        {
            sprite = TryLoadSprite(guide, imageName);
            if (sprite != null) return sprite;
        }

        int pageNum = _currentPageIndex + 1;
        string guideFolderName = !string.IsNullOrEmpty(guide.resourceFolderPath)
            ? System.IO.Path.GetFileName(guide.resourceFolderPath)
            : string.Empty;

        string guideIndexStr = string.Empty;
        if (!string.IsNullOrEmpty(guideFolderName) && guideFolderName.StartsWith("Guide", StringComparison.OrdinalIgnoreCase))
        {
            guideIndexStr = guideFolderName.Substring(5);
        }

        List<string> fallbackNames = new List<string>();
        if (!string.IsNullOrEmpty(guideIndexStr))
        {
            fallbackNames.Add($"{guideIndexStr}-{pageNum}");
        }
        fallbackNames.Add($"0-{pageNum}");
        fallbackNames.Add($"1-{pageNum}");
        fallbackNames.Add($"{pageNum}");

        foreach (var fallbackName in fallbackNames)
        {
            sprite = TryLoadSprite(guide, fallbackName);
            if (sprite != null) return sprite;
        }

        return null;
    }

    private Sprite TryLoadSprite(GuideData guide, string imageName)
    {
        if (string.IsNullOrEmpty(imageName))
            return null;

        string cleanName = imageName;
        if (cleanName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            cleanName = cleanName.Substring(0, cleanName.Length - 4);

        string[] candidateNames = cleanName == imageName
            ? new[] { cleanName }
            : new[] { cleanName, imageName };

        foreach (var name in candidateNames)
        {
            if (!string.IsNullOrEmpty(guide.resourceFolderPath))
            {
                Sprite sprite = Resources.Load<Sprite>($"{guide.resourceFolderPath}/Image/{name}");
                if (sprite != null) return sprite;

                sprite = Resources.Load<Sprite>($"{guide.resourceFolderPath}/{name}");
                if (sprite != null) return sprite;
            }

            Sprite directSprite = Resources.Load<Sprite>($"Guide/{name}");
            if (directSprite != null) return directSprite;

            directSprite = Resources.Load<Sprite>(name);
            if (directSprite != null) return directSprite;
        }

        return null;
    }

    public void OnCloseButtonClicked()
    {
        if (uiManager != null)
            uiManager.CloseGuidePanel();
        else
            Close();
    }

    public void OnExitButtonClicked()
    {
        OnCloseButtonClicked();
    }

    public void OnGuideButtonClicked()
    {
        if (uiManager != null)
            uiManager.ToggleGuidePanel();
        else
            Toggle();
    }

    public void OnPreviousButtonClicked()
    {
        PreviousPage();
    }

    public void OnNextButtonClicked()
    {
        NextPage();
    }
}
