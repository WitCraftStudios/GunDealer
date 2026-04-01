# Gun Black Market Simulator - Setup & Instruction Guide

This project contains all the necessary C# scripts to build the "Gun Black Market Simulator". Since the project uses Primitives, follow the instructions below to set up the scene in Unity.

## 1. Project & Folder Structure

Ensure your project folders look like this (create them if missing):
```
Assets/
├── Scripts/          <-- All provided C# scripts go here
├── Materials/        <-- Create materials for parts (Red, Green, Blue, etc.)
├── Prefabs/          <-- Drag your created objects here for reuse
├── Resources/        <-- (Optional)
└── Scenes/           <-- MainScene
```

## 2. Scene Setup Instructions (Step-by-Step)

### A. The Environment
1.  **Floor**: Create a **Plane** (Scale 2, 1, 2). Name it `Floor`. Apply a dark gray material.
    *   **Layer**: Add a Layer named "Ground". Set the Floor's layer to "Ground".
2.  **Walls**: Create **Cubes** to wall off the area so the player doesn't fall.

### B. The Player
1.  Create a **Capsule**. Name it `Player`. Position: (0, 1, 0).
2.  **Tag**: Set Tag to `Player`.
3.  **Rigidbody**: Add `Rigidbody` component.
    *   Mass: `60`
    *   Drag: `0`
    *   Angular Drag: `0.05`
    *   Constraints: Check **Freeze Rotation X, Y, Z**.
4.  **Camera**: Move the **Main Camera** to be a child of `Player`. Position: (0, 0.6, 0).
5.  **Scripts**: Add `PlayerController.cs`.
    *   `Ground Check`: Create an empty child under Player named `GroundCheck` at (0, -1, 0). Drag this to the script.
    *   `Ground Mask`: Set to "Ground" layer.
    *   `Interact Layer`: Set to "Default" (or create an "Interactable" layer if you prefer).
6.  **Hand**: Create an empty child under Player named `HandPos` at (0.5, 0.5, 1).
7.  **Inventory**: Add `PlayerInventory.cs`.
    *   `Hand Position`: Drag `HandPos` here.

### C. Managers
1.  Create an Empty GameObject named `GameManagers`.
2.  Add the following scripts to it:
    *   `OrderManager.cs`
    *   `CraftingManager.cs`
    *   `RewardManager.cs`
    *   `TimerManager.cs`

### D. Computer Station (Orders)
1.  **Desk**: Create a **Cube** (Scale 2, 1, 1).
2.  **PC**: Create a smaller **Cube** on top.
3.  **Interact**: Add `BoxCollider` (IsTrigger = true) slightly larger than the PC.
4.  **Script**: Add `ComputerInteract.cs`.
5.  **View Target**: Create an Empty GameObject named `ComputerCamPos` in front of the screen. Drag this to `OrderManager` -> `ComputerViewTarget`.

### E. Crafting Station
1.  **Table**: Create a **Cube** (Scale 3, 1, 1).
2.  **Slots**: Create 4 flattened Cubes (or Planes) on the table.
    *   Name them: `Slot_Grip`, `Slot_Trigger`, `Slot_Mag`, `Slot_Body`.
    *   Add `PartSlot.cs` to each.
    *   **Configure**: Set `Required Type` for each (e.g., `GripA`, `Trigger1`, `MagShort`, `BodyAK`).
    *   **Link**: Drag these 4 slots into the `CraftingManager` slots fields.
3.  **Assemble Button**: 
    *   Create a **Cube** (small, red). Name it `AssembleButton`.
    *   Add `AssembleButtonInteract.cs` script.
    *   Place it on the table.
    *   **Note**: No UI link needed anymore! Just press E on this cube when parts are ready.

### F. Parts & Storage
1.  Create **Materials**: Red (Grip), Green (Trigger), Blue (Mag), Yellow (Body).
2.  **Create Prefabs**:
    *   **Grip**: Cube (small). Script `Part.cs` (Type: `GripA`). Add `Rigidbody`.
    *   **Trigger**: Sphere (small). Script `Part.cs` (Type: `Trigger1`). Add `Rigidbody`.
    *   **Mag**: Cylinder. Script `Part.cs` (Type: `MagShort`). Add `Rigidbody`.
    *   **Body**: Cube (large). Script `Part.cs` (Type: `BodyAK`). Add `Rigidbody`.
3.  **Shelves**: Create Cubes acting as shelves. Place several Part prefabs on them.

### G. Conveyor Belt
1.  **Belt**: Long **Cube** (Scale 1, 0.1, 5).
2.  **Script**: Add `ConveyorInteract.cs`. Assign `ConveyorManager` ref.
3.  **Manager Setup**: On `GameManagers` (`ConveyorManager`):
    *   `Drop Point`: Empty GameObject at start of belt.
    *   `End Point`: Empty GameObject at end of belt.

### H. UI Setup
1.  **Canvas**: Create a Canvas (Scale With Screen Size).
2.  **Order Panel**: Create a Panel (initially inactive).
    *   Add **Text (TMP)** for Order Details. Link to `OrderManager`.
    *   Add **Buttons**: "Accept" (`OrderManager.AcceptOrder`), "Reject" (`OrderManager.RejectOrder`).
3.  **HUD**: Add Text for Cash (`RewardManager`).

## 3. Creating Updates (GunOrders)
1.  In Project window, right-click -> Create -> **GunBlackMarket** -> **GunOrder**.
2.  Create 3-5 orders (e.g., "Mafia Boss request", "Rebel Supply").
3.  Set their requirements (GripA, BodyAK, etc.) and Price.
4.  Drag these assets into `OrderManager` -> `Possible Orders`.

## 4. How to Test (Full Loop)
1.  **Play**. You spawn in warehouse.
2.  Walk to Computer. Press **E**.
3.  UI appears. Click **Accept**.
4.  Wait 1s. Blueprint spawns (if you set up `PrinterSpawnPoint`).
5.  Go to shelves. Hover over a **Grip**. Press **E** to pick up.
6.  Walk to Assembly Table. Aim at **Grip Slot**. Press **E**. It snaps.
7.  Repeat for Trigger, Mag, Body.
8.  **Assemble Button** appears. Click it.
9.  Gun spawns. You auto-hold it.
10. Pick up a **Gun Case** (Suitcase primitive).
11. Press **E** to snap gun into case.
12. Walk to Conveyor. Press **E**.
13. Case moves away. Cash Counter goes up!

## 5. Common Fixes
*   **Raycast not hitting?** Ensure objects have Colliders. Ensure Player is on different layer than "Interactable" if you use layer masks strictly.
*   **Falling through floor?** Check `GroundCheck` position (must be slightly below player pivot) and "Ground" Layer assignment.
*   **Cursor stuck?** Press Escape (if in editor) or ensure `Cursor.lockState` logic in `PlayerController` is working.
*   **Parts flying away?** Increase mass or drag on Rigidbodies. Tick "Interpolate" for smoother pickup movement.
## Phase 2: Shop System Setup

### 1. PC Manager (The New Brain)
1.  Create an empty GameObject named **`PCManager`**.
2.  Add the `PCManager.cs` script to it.
3.  **Assign References**:
    *   **PC Canvas**: Drag your existing `OrderUI` (the Canvas or Panel) here.
    *   **Order Tab**: Create a Panel inside your Canvas for Orders (move existing Order UI into it) and drag it here.
    *   **Shop Tab**: Create a NEW Panel for the Shop and drag it here.
    *   **Computer View Target**: Drag your `ComputerCamPos` object here.
    *   **Player Controller**: Drag your `Player` object here.

### 2. Shop UI (The Storefront)
1.  Inside your **Shop Tab** Panel:
    *   Create a **Grid Layout Group** (optional, for neatness).
    *   Create **Buttons** for each part you want to sell (e.g., "Grip Button").
    *   **On Click**: Add a new OnClick event.
        *   Drag **`ShopManager`** (see below) to the object field.
        *   Select `ShopManager` -> `PurchaseItem`.
        *   **Parameter**: You will need to drag the specific **ShopItem** ScriptableObject (see step 5) into the slot.

### 3. Shop Manager (The Cash Register)
1.  Create an empty GameObject named **`ShopManager`**.
2.  Add the `ShopManager.cs` script.
3.  **Catalog**: You can drag your ShopItems here if checking the catalog, but the logic primarily uses the Button links.

### 4. Delivery Zone (The Drop Point)
1.  Create an empty GameObject named **`DeliveryZone`** near the warehouse door.
2.  Add the `DeliveryZone.cs` script.
3.  **Spawn Point**: Drag the `DeliveryZone` object itself (or a child empty) into this field.

### 5. Creating Items (The Products)
1.  In your Project window, Right Click -> **Create** -> **GunBlackMarket** -> **ShopItem**.
2.  Name it (e.g., `Shop_GripA`).
3.  **Inspector Settings**:
    *   **Item Name**: "Vertical Grip"
    *   **Cost**: 50
    *   **Prefab**: Drag your `GripA_Prefab` here.
4.  **Important**: Repeat for all buyable parts.
5.  **Link**: Drag these Item files into the Button OnClick events you created in Step 2.

### 6. Updates to Existing Objects
*   **OrderManager**: You don't need to do anything; it now auto-delegates to PCManager.
*   **Computer Interact**: Ensure the `Computer` object's collider has the `ComputerInteract` script (it should default to calling PCManager now).
r pivot) and "Ground" Layer assignment.
*   **Cursor stuck?** Press Escape (if in editor) or ensure `Cursor.lockState` logic in `PlayerController` is working.
*   **Parts flying away?** Increase mass or drag on Rigidbodies. Tick "Interpolate" for smoother pickup movement.
