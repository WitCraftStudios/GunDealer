# Gun Black Market Simulator Deployment Brief

## 1. Original Prototype State

Before this improvement pass, the project already had a working prototype loop:

- First-person movement and object interaction
- Order acceptance and rejection from a computer
- Part pickup and placement on a crafting bench
- Gun assembly, gun casing, and conveyor delivery
- Reward payout, heat accumulation, day progression, and campaign failure
- A basic shop system with ScriptableObject-driven part items

The prototype was mechanically promising, but it still behaved like a development build in several important ways:

- Most player feedback existed only through `Debug.Log` messages
- The required parts for an order were not clearly shown in the order UI
- The player had no proper drop action and no reliable way to leave the PC except via specific UI actions
- Interaction depended heavily on scene layers and manual setup
- The shop depended on hand-wired UI instead of building from its catalog
- The order pool was very small, so repetition happened quickly
- The economy was very loose relative to order payouts

## 2. What Was Changed In This Pass

### Core Usability Improvements

- Added a new runtime gameplay overlay with:
  - center crosshair
  - world interaction prompt
  - top-screen toast notifications
  - contextual control hints
- Added a `GameFeedback` helper so important gameplay messages now appear on-screen instead of only in the console
- Added context-aware interaction prompts through a new `IInteractionPromptProvider` interface
- Improved player interaction targeting so the raycast is less brittle and no longer depends entirely on one interaction layer
- Added an actual drop action for held items
- Added proper `Esc` handling to exit the PC
- Resized the HUD at runtime so the expanded multi-line cash, heat, day, objective, and alert text is actually visible in the Game view
- Rebuilt the order board into a larger runtime layout so detailed order information is no longer clipped by the original tiny scene text box
- Fixed runtime summary overlays so day-start and campaign-status popups reliably render their TextMeshPro text

### PC and Shop Flow Improvements

- Added runtime PC navigation buttons for:
  - `Orders`
  - `Shop`
  - `Close`
- Added keyboard shortcuts inside the PC:
  - `1` for Orders
  - `2` for Shop
  - `Tab` to switch tabs
  - `Esc` to close the PC
- Rebuilt the shop into a runtime-generated catalog UI driven by the `ShopManager` catalog instead of depending on manually linked buttons
- Shop entries now clearly show:
  - item name
  - price
  - description
  - unlock status
  - affordability

### Order and Crafting Clarity

- Updated the order UI to explicitly show the required part recipe
- Added HUD objective text so the player always knows the next goal
- Added HUD recipe text for accepted orders
- Added HUD bench status text showing crafting progress
- Added clearer crafting feedback for:
  - missing parts
  - assembly readiness
  - correct build vs wrong build

### Runtime Stability and Deployment Readiness

- Added `RuntimeGameBootstrap` to ensure core runtime systems are created and configured consistently
- Added script metadata files for the new runtime support scripts
- Standardized screen-space canvas scaling for runtime-created UI

### Economy and Content Pass

- Rebalanced the starting cash upward to support a more meaningful shop economy
- Rebalanced existing order payouts downward to reduce trivial progression
- Rebalanced shop prices upward so buying parts carries weight
- Improved shop item descriptions so the store reads like an actual supplier interface
- Added 6 new order assets under `Assets/Resources/Orders`
- Updated `OrderManager` so it can merge scene-configured orders with resource-loaded orders

## 3. New And Improved Feature Set

The game now supports the following playable loop more clearly and more reliably:

1. Walk around the warehouse and interact with objects using clearer prompts
2. Use the computer to inspect available orders
3. Switch between Orders and Shop inside the PC
4. Buy parts from a generated shop catalog
5. Collect parts and assemble the requested weapon at the bench
6. Load the finished gun into a case
7. Ship the case on the conveyor
8. Receive a reward with clearer payout feedback
9. Manage heat, inspections, raids, days, and campaign failure across runs

Additional player-facing improvements now included:

- On-screen feedback for invalid actions
- Better readability of police-pressure events
- Better visibility of day transitions
- More order variety and less immediate repetition

## 4. Data Changes Made

### Existing Orders Rebalanced

- `Order_Pistol`
- `Order_Rifle`
- `Order_AK47`

These were tuned for lower payout inflation and slightly more measured risk/reward.

### Shop Rebalance

These shop items were re-priced and re-described:

- `Shop_BodyAK`
- `Shop_BodyAR`
- `Shop_GripA`
- `Shop_GripB`
- `Shop_Mag_Long`
- `Shop_Mag_Short 1`
- `Shop_Trigger1`
- `Shop_Trigger2`

### Newly Added Orders

- `Order_BackAlleyPistol`
- `Order_RunnerSidearm`
- `Order_CourierRifle`
- `Order_MarketGuard`
- `Order_CrateBreaker`
- `Order_BorderlineAK`

## 5. Main Code Files Added

- `Assets/Scripts/GameplayUIManager.cs`
- `Assets/Scripts/GameFeedback.cs`
- `Assets/Scripts/RuntimeGameBootstrap.cs`
- `Assets/Scripts/RuntimeUiFactory.cs`

## 6. Main Code Files Updated

- `Assets/Scripts/PlayerController.cs`
- `Assets/Scripts/PlayerInventory.cs`
- `Assets/Scripts/Part.cs`
- `Assets/Scripts/PartSlot.cs`
- `Assets/Scripts/GunCase.cs`
- `Assets/Scripts/ConveyorInteract.cs`
- `Assets/Scripts/ConveyorManager.cs`
- `Assets/Scripts/ComputerInteract.cs`
- `Assets/Scripts/AssembleButtonInteract.cs`
- `Assets/Scripts/OrderManager.cs`
- `Assets/Scripts/CraftingManager.cs`
- `Assets/Scripts/RewardManager.cs`
- `Assets/Scripts/PCManager.cs`
- `Assets/Scripts/ShopManager.cs`
- `Assets/Scripts/TimerManager.cs`
- `Assets/Scripts/DayManager.cs`
- `Assets/Scripts/HeatManager.cs`
- `Assets/Scripts/IInteractable.cs`

## 7. Remaining Caveats

The game is much more deployment-ready than before, but a few limitations still remain:

- I could not perform a true C# compile locally because this machine does not have `dotnet`, `msbuild`, `csc`, or `mcs` installed
- I did not run Unity Play Mode directly from this environment
- The project still depends on the current scene and prefab setup being valid in Unity
- The visual/art polish is still prototype-level even though the usability is much stronger

## 8. Recommended Final Pre-Deployment Checks In Unity

- Open the project once so Unity imports the new scripts and assets
- Enter [Assets/Scenes/DeploymentScene.unity](/home/samyog/Projects/Unity/GunBlackMarketSim/BlackMarket/GunBlackMarketSim/Assets/Scenes/DeploymentScene.unity)
- Enter Play Mode and verify:
  - the top-right HUD is readable and no longer clipped
  - the day-start summary popup appears with visible text
  - interaction prompts appear correctly
  - the PC can switch between Orders and Shop
  - the Orders tab shows the large runtime order board with readable details
  - shop purchases spawn at the delivery zone
  - crafting HUD updates as parts are placed
  - delivery payouts and heat updates behave as expected
- Confirm the newly added resource orders are appearing in rotation
- Confirm UI layout looks good at your target resolution

## 9. Outcome

This pass turns the project from a promising prototype into a much more readable, playable, and presentable deployment build. The biggest gain is not just new content, but the removal of prototype friction:

- the player now understands what to do
- the shop is now usable
- the order requirements are visible
- the controls are more complete
- the runtime systems are more self-healing
- the content pool and economy are both more suitable for an actual playable submission
