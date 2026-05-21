using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Menu_Advanced : MonoBehaviour
{
  enum UILevel { RecipeSelection, IngredientSelection }

  [Header("References")]
  [SerializeField] private CanBeCraftIn currentStation;

  [Header("Left Panel")]
  [SerializeField] private GameObject leftPanel;
  [SerializeField] private Transform menuSlotContainer;
  [SerializeField] private GameObject menuSlotTemplate;
  [SerializeField] private Transform recipeDetailContainer;
  [SerializeField] private GameObject recipeDetailTemplate;

  [Header("Right Panel")]
  [SerializeField] private GameObject rightPanel;
  [SerializeField] public GameObject cookingMenu; // root of the recipe selection UI (assign the panel that holds all recipe/menu controls)
  [SerializeField] private Image IconBg;
  [SerializeField] private Image recipeImage;
  [SerializeField] private TMP_Text recipeNameText;
  [SerializeField] private Transform ingredientBoxContainer;
  [SerializeField] private GameObject ingredientBoxTemplate;
  [SerializeField] private Button craftButton;
  [SerializeField] private Transform ingredientStackContainer;
  [SerializeField] private GameObject ingredientStackTemplate;
  [SerializeField] private Button backButton;

  private ItemDatabaseSO itemDatabase;
  private InventoryController playerInventory;
  private Item currentRecipe;
  private UILevel currentLevel = UILevel.RecipeSelection;

  // Track selections: Dictionary<RequiredItemSO, List<(int slotIndex, int amount)>>
  private Dictionary<RequiredItemSO, List<(int slotIndex, int amount)>> currentSelections;
  private RequiredItemSO expandedIngredient;

  // Track per-box stack selections: List of (RequiredItemSO, selectedSlotIndex) for each ingredient box
  private List<(RequiredItemSO req, int slotIndex)> boxStackSelections;
  private int currentBoxIndex = -1;

  // Track which boxes have been configured (not auto-allocated)
  private List<bool> boxHasUserSelection;

  private void Awake()
  {
    itemDatabase = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
    if (itemDatabase == null)
    {
      Debug.LogError("ItemDatabaseSO not found in Resources/Database!");
      return;
    }

    playerInventory = GameManager.instance.player.GetComponent<Player>().GetInventoryController();
    playerInventory.OnInventoryUpdated += OnInventoryChanged;

    // Hide templates
    menuSlotTemplate.SetActive(false);
    recipeDetailTemplate.SetActive(false);
    ingredientBoxTemplate.SetActive(false);
    ingredientStackTemplate.SetActive(false);

    craftButton.onClick.AddListener(OnCraftClicked);
    if (backButton != null) backButton.onClick.AddListener(GoBackToRecipeLevel);

    currentSelections = new Dictionary<RequiredItemSO, List<(int, int)>>();
  }

  private void Start()
  {
    ShowRecipeLevel();
  }

  // === LEVEL 0: Recipe Selection ===
  private void ShowRecipeLevel()
  {
    currentLevel = UILevel.RecipeSelection;

    // LEFT: Recipe Grid
    ClearContainer(menuSlotContainer);
    Vector2 startPos = menuSlotTemplate.transform.GetComponent<RectTransform>().anchoredPosition;
    int x = 0, y = 0;
    float cellSize = 116f;

    foreach (ItemSO itemSO in itemDatabase.allItems)
    {
      if (itemSO.requiredItems == null || itemSO.requiredItems.Count == 0) continue;
      if (itemSO.stationType != currentStation) continue;

      Item recipeItem = new Item(itemSO);
      RectTransform slot = Instantiate(menuSlotTemplate, menuSlotContainer).GetComponent<RectTransform>();
      slot.gameObject.SetActive(true);

      slot.Find("Image").GetComponent<Image>().sprite = recipeItem.GetSprite();
      slot.anchoredPosition = startPos + new Vector2(x * cellSize, -y * cellSize);

      Button_UI btn = slot.GetComponent<Button_UI>();
      if (btn != null)
      {
        btn.ClickFunc = () => ShowRecipeDetail(recipeItem);
      }

      x++;
      if (x > 5)
      {
        x = 0;
        y++;
      }
    }

    // RIGHT: Clear and hide all right panel content
    craftButton.gameObject.SetActive(false);
    ClearContainer(ingredientBoxContainer);
    ClearContainer(ingredientStackContainer);
    if (recipeImage != null)
    {
      recipeImage.gameObject.SetActive(false);
      recipeImage.sprite = null;
    }
    if (recipeNameText != null) recipeNameText.text = "";
    IconBg.gameObject.SetActive(false);
    if (backButton != null) backButton.gameObject.SetActive(false);
  }

  // === Show Recipe Detail on Right (Level 0) ===
  private void ShowRecipeDetail(Item recipe)
  {
    // Only clear state if it's a DIFFERENT recipe
    if (currentRecipe == null || currentRecipe.itemSO != recipe.itemSO)
    {
      boxStackSelections = null;
      boxHasUserSelection = null;
      currentSelections.Clear();
    }
    currentRecipe = recipe;

    // RIGHT PANEL: Recipe Details + Ingredient Boxes
    IconBg.gameObject.SetActive(true);
    if (recipeImage != null) recipeImage.gameObject.SetActive(true);
    recipeImage.sprite = recipe.GetSprite();
    recipeNameText.text = recipe.itemSO.GetDisplayName();

    // Clear any leftover stacks from Level 1
    ClearContainer(ingredientStackContainer);
    ClearContainer(ingredientBoxContainer);

    // Initialize per-box selections tracking (only if new recipe)
    if (boxStackSelections == null || boxStackSelections.Count == 0)
    {
      boxStackSelections = new List<(RequiredItemSO, int)>();
      boxHasUserSelection = new List<bool>();

      // Auto-allocate selections for each box, respecting what's already used
      int boxIndex = 0;
      foreach (RequiredItemSO req in recipe.itemSO.requiredItems)
      {
        List<int> eligibleSlots = playerInventory.GetSlotsWithItem(req.item, req.minQuality);

        for (int i = 0; i < req.amount; i++)
        {
          int allocatedSlot = FindNextAvailableSlot(req, eligibleSlots, boxIndex);
          boxStackSelections.Add((req, allocatedSlot));
          boxHasUserSelection.Add(false); // Not user-selected, auto-allocated
          boxIndex++;
        }
      }
    }

    // Create one ingredient box per required amount
    int totalBoxIndex = 0;
    foreach (RequiredItemSO req in recipe.itemSO.requiredItems)
    {
      List<int> eligibleSlots = playerInventory.GetSlotsWithItem(req.item, req.minQuality);
      bool hasIngredient = eligibleSlots != null && eligibleSlots.Count > 0;

      for (int i = 0; i < req.amount; i++)
      {
        RectTransform box = Instantiate(ingredientBoxTemplate, ingredientBoxContainer).GetComponent<RectTransform>();
        box.gameObject.SetActive(true);

        // Get the allocated slot for this box
        int selectedSlotIndex = -1;
        if (totalBoxIndex < boxStackSelections.Count)
        {
          selectedSlotIndex = boxStackSelections[totalBoxIndex].slotIndex;
        }

        Image boxImage = box.Find("Image")?.GetComponent<Image>();
        if (boxImage != null)
        {
          if (selectedSlotIndex >= 0)
          {
            Item selectedItem = playerInventory.GetItemAt(selectedSlotIndex);
            if (selectedItem != null)
            {
              boxImage.sprite = selectedItem.GetSprite();
              boxImage.color = Color.white;
            }
            else
            {
              boxImage.sprite = req.item.icon;
              boxImage.color = hasIngredient ? Color.white : new Color(1, 1, 1, 0.3f);
            }
          }
          else
          {
            // INVALID ALLOCATION: No slot could be allocated (player doesn't have enough items)
            boxImage.sprite = req.item.icon;
            boxImage.color = new Color(1, 1, 1, 0.3f);
          }
        }

        TMP_Text nameText = box.Find("Text_Name")?.GetComponent<TMP_Text>();
        if (nameText != null) nameText.text = req.item.GetDisplayName();

        // Display selected amount (how many this box selected)
        TMP_Text amountText = box.Find("Text_Amount")?.GetComponent<TMP_Text>();
        if (amountText != null)
        {
          amountText.text = "1"; // Each box represents 1 unit selected
        }

        // Display quality in Text_Selected field
        TMP_Text selectedText = box.Find("Text_Selected")?.GetComponent<TMP_Text>();
        if (selectedText != null)
        {
          if (selectedSlotIndex >= 0)
          {
            Item selectedItem = playerInventory.GetItemAt(selectedSlotIndex);
            if (selectedItem != null)
            {
              selectedText.text = selectedItem.quality.ToString();
            }
          }
          else
          {
            selectedText.text = "";
          }
        }

        Button boxButton = box.GetComponent<Button>();
        if (boxButton != null && hasIngredient)
        {
          int capturedBoxIndex = totalBoxIndex;
          // Only allow clicking if this box has a valid allocation
          bool isValidAllocation = selectedSlotIndex >= 0;
          boxButton.interactable = isValidAllocation;
          if (isValidAllocation)
          {
            boxButton.onClick.AddListener(() => ShowIngredientLevel(req, capturedBoxIndex));
          }
        }

        totalBoxIndex++;
      }
    }

    // Rebuild selections from auto-allocations so craft button state is correct
    RebuildSelectionsFromBoxes();

    craftButton.gameObject.SetActive(true);
    backButton.gameObject.SetActive(false);
    RefreshCraftButton();
  }

  // Find the next available slot for this box, avoiding already-selected slots
  // Explicitly finds the HIGHEST QUALITY available slot
  private int FindNextAvailableSlot(RequiredItemSO req, List<int> eligibleSlots, int boxIndex)
  {
    if (eligibleSlots == null || eligibleSlots.Count == 0) return -1;
    // Count how many units of each slot are already allocated by earlier boxes (only indices < boxIndex)
    Dictionary<int, int> usedCount = new Dictionary<int, int>();
    for (int i = 0; i < boxIndex && i < boxStackSelections.Count; i++)
    {
      if (boxStackSelections[i].req == req && boxStackSelections[i].slotIndex >= 0)
      {
        int s = boxStackSelections[i].slotIndex;
        if (!usedCount.ContainsKey(s)) usedCount[s] = 0;
        usedCount[s]++;
      }
    }

    // Find the highest-quality slot that still has remaining quantity (amount - usedCount > 0)
    int bestSlot = -1;
    int bestQuality = int.MinValue;
    foreach (int slot in eligibleSlots)
    {
      Item slotItem = playerInventory.GetItemAt(slot);
      if (slotItem == null) continue;

      int alreadyUsed = usedCount.ContainsKey(slot) ? usedCount[slot] : 0;
      int remaining = slotItem.amount - alreadyUsed;
      if (remaining <= 0) continue; // no units left in this slot for allocation

      int slotQuality = (int)slotItem.quality;
      if (slotQuality > bestQuality)
      {
        bestQuality = slotQuality;
        bestSlot = slot;
      }
    }

    return bestSlot;
  }

  // === LEVEL 1: Ingredient Selection ===
  private void ShowIngredientLevel(RequiredItemSO req, int boxIndex = -1)
  {
    currentLevel = UILevel.IngredientSelection;
    expandedIngredient = req;
    currentBoxIndex = boxIndex;

    // RIGHT: Clear ingredient boxes so stacks can display without overlapping
    ClearContainer(ingredientBoxContainer);
    if (recipeImage != null) recipeImage.gameObject.SetActive(false);
    if (recipeNameText != null) recipeNameText.text = "";

    // LEFT: Move recipe detail here (with slide animation from right -> left)
    ClearContainer(recipeDetailContainer);
    RectTransform recipeBox = Instantiate(recipeDetailTemplate, recipeDetailContainer).GetComponent<RectTransform>();
    recipeBox.gameObject.SetActive(true);

    Image recipeBoxImage = recipeBox.Find("Image")?.GetComponent<Image>();
    if (recipeBoxImage != null) recipeBoxImage.sprite = currentRecipe?.GetSprite();

    TMP_Text recipeBoxName = recipeBox.Find("Text_Name")?.GetComponent<TMP_Text>();
    if (recipeBoxName != null) recipeBoxName.text = currentRecipe?.itemSO.GetDisplayName() ?? "";

    // Show ingredients as small list or text
    TMP_Text recipeBoxDesc = recipeBox.Find("Text_Description")?.GetComponent<TMP_Text>();
    if (recipeBoxDesc != null)
    {
      string ingredientList = "Ingredients:\n";
      if (currentRecipe != null)
      {
        foreach (var r in currentRecipe.itemSO.requiredItems)
        {
          if (r == null || r.item == null) continue;
          ingredientList += $"- {r.item.GetDisplayName()} x{r.amount}\n";
        }
      }
      recipeBoxDesc.text = ingredientList;
    }

    // RIGHT: Show stacks for this ingredient
    ClearContainer(ingredientStackContainer);

    List<int> eligibleSlots = playerInventory.GetSlotsWithItem(req.item, req.minQuality);

    if (eligibleSlots == null || eligibleSlots.Count == 0)
    {
      GameObject noItemObj = Instantiate(ingredientStackTemplate, ingredientStackContainer);
      noItemObj.SetActive(true);

      TMP_Text noItemText = noItemObj.GetComponent<TMP_Text>();
      if (noItemText == null)
      {
        noItemText = noItemObj.GetComponentInChildren<TMP_Text>(true);
      }

      if (noItemText != null)
      {
        noItemText.text = "No eligible items in inventory";
      }

      craftButton.gameObject.SetActive(false);
      IconBg.gameObject.SetActive(false);
      if (backButton != null) backButton.gameObject.SetActive(true);
      return;
    }

    // Get the currently selected slot for this box
    int currentlySelectedSlot = -1;
    if (boxIndex >= 0 && boxIndex < boxStackSelections.Count)
    {
      currentlySelectedSlot = boxStackSelections[boxIndex].slotIndex;
    }

    foreach (int slotIndex in eligibleSlots)
    {
      Item slotItem = playerInventory.GetItemAt(slotIndex);
      if (slotItem == null) continue;

      RectTransform stackOption = Instantiate(ingredientStackTemplate, ingredientStackContainer).GetComponent<RectTransform>();
      stackOption.gameObject.SetActive(true);

      Image stackImage = stackOption.Find("Image")?.GetComponent<Image>();
      if (stackImage != null) stackImage.sprite = slotItem.GetSprite();

      TMP_Text qualityText = stackOption.Find("Text_Quality")?.GetComponent<TMP_Text>();
      if (qualityText != null) qualityText.text = slotItem.quality.ToString();

      Image backgroundQuality = stackOption.Find("BackgroundQualityParent/BackgroundQuality").GetComponent<Image>();
      if (backgroundQuality != null) backgroundQuality.color = ItemSO.GetQualityColor(slotItem.quality, slotItem.itemSO != null && slotItem.itemSO.UsesQuality());

      // Show selected amount from THIS box (top) and from OTHER boxes (indicator)
      TMP_Text selectedText = stackOption.Find("Text_Selected")?.GetComponent<TMP_Text>();
      TMP_Text amountText = stackOption.Find("Text_Amount")?.GetComponent<TMP_Text>();

      bool isSelected = (slotIndex == currentlySelectedSlot);

      // Count how many other boxes are using this slot
      int otherBoxesUsingThisSlot = 0;
      for (int i = 0; i < boxStackSelections.Count; i++)
      {
        if (i != boxIndex && boxStackSelections[i].req == req && boxStackSelections[i].slotIndex == slotIndex)
        {
          otherBoxesUsingThisSlot++;
        }
      }

      // Display: Selected_Text shows selected count (this box + others), Amount_Text shows remaining
      // Bubble shows when ANY box selected it (including other boxes)
      if (isSelected)
      {
        int totalSelected = 1 + otherBoxesUsingThisSlot;
        int remaining = Mathf.Max(0, slotItem.amount - totalSelected);
        if (selectedText != null) selectedText.text = totalSelected.ToString(); // Total selected by all boxes
        if (amountText != null) amountText.text = remaining.ToString(); // Remaining amount

        // Bubble shows when THIS box selected it
        GameObject bubbleSelected = stackOption.Find("SelectBubble")?.gameObject;
        if (bubbleSelected != null) bubbleSelected.SetActive(true);
      }
      else if (otherBoxesUsingThisSlot > 0)
      {
        int remaining = Mathf.Max(0, slotItem.amount - otherBoxesUsingThisSlot);
        if (selectedText != null) selectedText.text = otherBoxesUsingThisSlot.ToString(); // Selected by other boxes
        if (amountText != null) amountText.text = remaining.ToString(); // Remaining amount

        // Bubble ALSO shows when OTHER boxes use it
        GameObject bubbleSelected = stackOption.Find("SelectBubble")?.gameObject;
        if (bubbleSelected != null) bubbleSelected.SetActive(true);
      }
      else
      {
        if (selectedText != null) selectedText.text = ""; // Nothing selected
        if (amountText != null) amountText.text = slotItem.amount.ToString(); // Total amount

        // NO bubble when nothing selected
        GameObject bubbleSelected = stackOption.Find("SelectBubble")?.gameObject;
        if (bubbleSelected != null) bubbleSelected.SetActive(false);
      }

      // Clicking the stack selects it for this box
      Button stackBtn = stackOption.GetComponent<Button>();
      if (stackBtn != null)
      {
        int capturedIndex = slotIndex;

        // Determine if this slot still has available units for selection by this box
        bool selectableForThisBox = isSelected || (otherBoxesUsingThisSlot < slotItem.amount);
        stackBtn.interactable = selectableForThisBox;

        if (selectableForThisBox)
        {
          stackBtn.onClick.AddListener(() =>
          {
            SelectStackForBox(req, capturedIndex, boxIndex);
            ShowIngredientLevel(req, boxIndex);
          });
        }
      }

      // Visual feedback: border highlight
      GameObject border = stackOption.Find("Border")?.gameObject;
      // Image borderImage = border.GetComponent<Image>();
      if (border != null) //  && borderImage != null
      {
        border.SetActive(false);
        if (isSelected)
        {
          border.SetActive(true);
          // borderImage.color = new Color(0, 1, 0, 0.8f); // Green when THIS box selected
        }
        else if (otherBoxesUsingThisSlot > 0)
        {
          // do nothing for now
          // borderImage.color = new Color(1, 1, 0, 0.5f); // Yellow if used by other boxes
        }
        else
        {
          // borderImage.color = new Color(1, 1, 1, 0.1f); // Dim if not used
        }
        // If slot is fully used by others and this box can't select it, dim further
        int availableForThisBox = slotItem.amount - otherBoxesUsingThisSlot;
        if (availableForThisBox <= 0 && !isSelected)
        {
          // borderImage.color = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        }
      }
    }

    craftButton.gameObject.SetActive(false);
    IconBg.gameObject.SetActive(false);
    if (backButton != null) backButton.gameObject.SetActive(true);
  }

  // Select a specific stack for a specific ingredient box
  private void SelectStackForBox(RequiredItemSO req, int slotIndex, int boxIndex)
  {
    if (boxIndex >= 0 && boxIndex < boxStackSelections.Count)
    {
      boxStackSelections[boxIndex] = (req, slotIndex);
      if (boxIndex < boxHasUserSelection.Count)
      {
        boxHasUserSelection[boxIndex] = true; // Mark as user-selected
      }

      // Rebuild selections based on current box allocations
      RebuildSelectionsFromBoxes();
      RefreshCraftButton();
    }
  }

  // Rebuild the currentSelections dictionary from boxStackSelections
  private void RebuildSelectionsFromBoxes()
  {
    currentSelections.Clear();

    foreach (var (req, slot) in boxStackSelections)
    {
      if (req == null || slot < 0) continue;

      if (!currentSelections.ContainsKey(req))
        currentSelections[req] = new List<(int, int)>();

      // Check if this slot is already in selections for this requirement
      bool found = false;
      for (int i = 0; i < currentSelections[req].Count; i++)
      {
        if (currentSelections[req][i].slotIndex == slot)
        {
          var sel = currentSelections[req][i];
          currentSelections[req][i] = (slot, sel.amount + 1);
          found = true;
          break;
        }
      }
      if (!found)
      {
        currentSelections[req].Add((slot, 1));
      }
    }
  }

  private void GoBackToRecipeLevel()
  {
    if (currentLevel == UILevel.IngredientSelection)
    {
      ShowRecipeDetail(currentRecipe); // redraw right panel with updated ingredient boxes
    }
  }

  private void RefreshCraftButton()
  {
    if (currentRecipe == null)
    {
      craftButton.interactable = false;
      return;
    }

    bool canCraft = true;
    foreach (var req in currentRecipe.itemSO.requiredItems)
    {
      int selectedAmount = 0;
      if (currentSelections.ContainsKey(req))
      {
        foreach (var sel in currentSelections[req]) selectedAmount += sel.amount;
      }

      if (selectedAmount < req.amount)
      {
        canCraft = false;
        break;
      }
    }

    craftButton.interactable = canCraft;
  }

  // React to changes in inventory so UI stays up-to-date
  private void OnInventoryChanged()
  {
    if (currentLevel == UILevel.IngredientSelection && expandedIngredient != null && currentBoxIndex >= 0)
    {
      ShowIngredientLevel(expandedIngredient, currentBoxIndex);
    }
    else if (currentLevel == UILevel.RecipeSelection && currentRecipe != null)
    {
      ShowRecipeDetail(currentRecipe);
    }
  }

  private void OnCraftClicked()
  {
    if (currentRecipe == null) return;

    // gather selections into a flat list
    List<(int slotIndex, int amount)> selections = new List<(int, int)>();
    List<float> qualityValues = new List<float>();

    foreach (var kv in currentSelections)
    {
      foreach (var sel in kv.Value)
      {
        selections.Add(sel);
        // record quality for each unit so we can compute an average later
        Item slotItem = playerInventory.GetItemAt(sel.slotIndex);
        if (slotItem != null)
        {
          for (int i = 0; i < sel.amount; i++)
            qualityValues.Add(slotItem.GetQualityValue());
        }
      }
    }

    // compute average quality of the ingredients
    float avgQuality = 1f;
    if (qualityValues.Count > 0)
    {
      float sum = 0f;
      foreach (var v in qualityValues) sum += v;
      avgQuality = sum / qualityValues.Count;
    }

    // find the cooking session on a parent (or sibling) object
    CookingSession session = GetComponentInParent<CookingSession>();
    GameObject canvas = null;
    if (session != null)
    {
      canvas = session.gameObject;
      session.BeginCooking(currentRecipe.itemSO, selections, avgQuality);
    }
    else
    {
      // fallback: try ancestor lookup manually
      canvas = transform.parent != null ? transform.parent.gameObject : null;
      if (canvas != null)
      {
        session = canvas.GetComponent<CookingSession>() ?? canvas.GetComponentInChildren<CookingSession>();
        if (session != null)
        {
          canvas = session.gameObject;
          session.BeginCooking(currentRecipe.itemSO, selections, avgQuality);
        }
      }
      if (session == null)
      {
        Debug.LogWarning("UI_Menu_Advanced: no CookingSession found on parent or ancestors");
      }
    }

    // hide only the recipe menu panel; leave the root canvas active so
    // timer/indicator texts remain visible while cooking.  If the panel
    // reference is missing or points at the root canvas, fall back to
    // disabling the whole canvas (makes sure UI actually disappears).
    if (cookingMenu == null)
    {
      Debug.LogWarning("UI_Menu_Advanced: cookingMenu reference is not set");
    }
    if (cookingMenu != null && cookingMenu != canvas)
    {
      cookingMenu.SetActive(false);
    }
    else if (canvas != null)
    {
      canvas.SetActive(false);
    }

    // make sure the game is unpaused and state tracker knows the window closed
    Time.timeScale = 1f;
    if (UI_StateManager.Instance != null)
    {
      UI_StateManager.Instance.interactWindowOpen = false;
    }
  }

  private void ClearContainer(Transform container)
  {
    foreach (Transform child in container)
    {
      if (child.gameObject == menuSlotTemplate || child.gameObject == recipeDetailTemplate ||
          child.gameObject == ingredientBoxTemplate || child.gameObject == ingredientStackTemplate)
        continue;
      Destroy(child.gameObject);
    }
  }

  private void OnDisable()
  {
    // Clear recipe selection and ingredient state, but keep recipe menu intact for reopen
    currentRecipe = null;
    boxStackSelections = null;
    boxHasUserSelection = null;
    currentLevel = UILevel.RecipeSelection;

    if (currentSelections != null)
    {
      currentSelections.Clear();
    }

    // Clear recipe detail and ingredient containers, but NOT the recipe menu
    ClearContainer(recipeDetailContainer);
    ClearContainer(ingredientBoxContainer);
    ClearContainer(ingredientStackContainer);

    // Deactivate UI elements
    if (recipeImage != null)
    {
      recipeImage.gameObject.SetActive(false);
      recipeImage.sprite = null;
    }
    if (IconBg != null) IconBg.gameObject.SetActive(false);
    if (craftButton != null) craftButton.gameObject.SetActive(false);
    if (backButton != null) backButton.gameObject.SetActive(false);
    if (recipeNameText != null) recipeNameText.text = "";

    if (playerInventory != null)
    {
      playerInventory.OnInventoryUpdated -= OnInventoryChanged;
    }
  }
}
