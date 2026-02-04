# Nalu.Reactor.Navigation - Flows Documentation

This document describes the three core flows in the Nalu.Reactor.Navigation system: Page Creation, Navigation, and Page Disposal.

---

## 1. Page Creation Flow

### Overview
Pages are created through two different mechanisms depending on whether they are root content pages or navigation stack pages.

### 1.1 Root Content Page Creation

**Trigger:** Application initialization or navigation to a root ShellContent

**Location:** `ShellInfo/ShellContentProxy.GetOrCreateContent()`

#### Flow Steps:

1. **Access ShellContent**
   ```csharp
   var contentProxy = shellProxy.GetContent(segmentName);
   ```

2. **Get or Create Native Page**
   ```csharp
   var page = ((IShellContentController)_content).GetOrCreateContent();
   ```
   - MAUI Shell's internal `GetOrCreateContent()` method creates the native MAUI Page
   - This uses the DataTemplate registered via `Navigation.PageTypeProperty`
   - The DataTemplate invokes `ShellNaluReactorComponentRouteFactory.GetOrCreate()`

3. **Route Factory Creates MauiReactor Component**
   **Location:** `Internals/ShellNaluReactorComponentRouteFactory.GetOrCreate()`
   
   ```csharp
   public override Element GetOrCreate()
   {
       var component = item.Item1;
       var propsInit = item.Item2;
       var pageComponent = MauiReactor.TemplateHost.Create(component);
       
       if (propsInit is not default(Action<object>))
       {
           // Initialize props if provided
           propsInit.Invoke(propsProp.GetValue(component));
       }
       
       var page = pageComponent.NativeElement;
       
       // Set reference from Page to Component
       page.SetValue(
           Reactor.ReactorBindableProperties.PageComponentReferenceProperty,
           new WeakReference<MauiReactor.Component>(component));
       
       return (Element)page;
   }
   ```

4. **Circular Reference Established:**
   - `MauiReactor.Component` → `_containerPage: WeakReference<Page>` (strong reference to Page via WeakReference wrapper)
   - `Native Page` → `PageComponentReferenceProperty: WeakReference<MauiReactor.Component>`

5. **Retrieve Component from Page**
   ```csharp
   var pageComponentWeakRef = (WeakReference<MauiReactor.Component>)
       page.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
   
   if (pageComponentWeakRef.TryGetTarget(out var pageComponent))
       return (pageComponent, page);
   ```

6. **Send Lifecycle Events**
   ```csharp
   await NavigationHelper.SendEnteringAsync(ShellProxy, component, Configuration);
   await NavigationHelper.SendAppearingAsync(ShellProxy, component, Configuration);
   ```

---

### 1.2 Navigation Stack Page Creation

**Trigger:** User navigates to a new page (push operation)

**Location:** `Internals/NavigationService.ExecuteRelativeNavigationAsync()`

#### Flow Steps:

1. **Create Component via Dependency Injection**
   ```csharp
   var component = CreatePage(pageType, stackPage.PageComponent);
   ```

2. **CreatePage Method**
   **Location:** `Internals/NavigationService.CreatePage()`
   
   ```csharp
   internal MauiReactor.Component CreatePage(Type pageType, MauiReactor.Component? parentPage)
   {
       // Create service scope for this page
       var serviceScope = ServiceProvider.CreateScope();
       
       // Get navigation service provider
       var navigationServiceProvider = serviceScope.ServiceProvider
           .GetRequiredService<INavigationServiceProviderInternal>();
       
       // Set parent relationship for scoped services
       if (parentPage is not default(MauiReactor.Component) && 
           PageNavigationContext.Get(parentPage) is { ServiceScope: { } parentScope})
       {
           var parentNavigationServiceProvider = parentScope.ServiceProvider
               .GetRequiredService<INavigationServiceProviderInternal>();
           navigationServiceProvider.SetParent(parentNavigationServiceProvider);
       }
       
       // Resolve component from DI container
       var reactorComponent = (MauiReactor.Component)serviceScope.ServiceProvider
           .GetRequiredService(pageType);
       
       // Set context page for navigation
       navigationServiceProvider.SetContextPage(reactorComponent);
       
       // Configure back button behavior
       var isRoot = parentPage is default(MauiReactor.Component);
       ConfigureBackButtonBehavior(reactorComponent, isRoot);
       
       // Store navigation context
       var pageContext = new PageNavigationContext(serviceScope);
       PageNavigationContext.Set(reactorComponent, pageContext);
       
       return reactorComponent;
   }
   ```

3. **Determine Presentation Mode**
   ```csharp
   var presentationMode = GetAttachedPropertiesField(component)
       .GetValueOrDefault(Shell.PresentationModeProperty);
   var isModal = presentationMode is not default(object) && 
       ((PresentationMode)presentationMode).HasFlag(PresentationMode.Modal);
   ```

4. **Send Entering Event**
   ```csharp
   await NavigationHelper.SendEnteringAsync(ShellProxy, component, Configuration);
   ```

5. **Push to Navigation Stack**
   ```csharp
   await shellProxy.PushAsync(segmentName, component, navigation.PropsDelegate);
   ```

6. **PushAsync Implementation**
   **Location:** `ShellInfo/ShellProxy.PushAsync()`
   
   ```csharp
   public Task PushAsync(string segmentName, MauiReactor.Component component, Action<object> propsInit)
   {
       var baseRoute = _navigationTarget ?? _shell.CurrentState.Location.OriginalString;
       var finalRoute = $"{baseRoute}/{segmentName}";
       
       // Register route factory with component
       var pageTypeRouteFactory = _routeFactory.GetRouteFactory(component.GetType());
       pageTypeRouteFactory.Push(component, propsInit);
       
       // Register route with MAUI's routing system
       if (!_registeredSegments.Contains(segmentName))
       {
           Routing.UnRegisterRoute(segmentName);
           Routing.RegisterRoute(segmentName, pageTypeRouteFactory);
           _registeredSegments.Add(segmentName);
       }
       
       // Update navigation target (triggers Shell navigation)
       _navigationTarget = finalRoute;
       
       return Task.CompletedTask;
   }
   ```

7. **Shell Navigates to Route**
   - MAUI Shell receives the `_navigationTarget` change
   - Calls the registered `pageTypeRouteFactory`
   - Factory invokes `MauiReactor.TemplateHost.Create(component)` to create native Page
   - Sets `PageComponentReferenceProperty` (WeakReference to Component)
   - Page is pushed to navigation stack

8. **Add to Internal Stack Tracking**
   ```csharp
   stack.Add(new NavigationStackPage(
       $"{stackPage.Route}/{segmentName}", 
       segmentName, 
       component, 
       isModal));
   ```

9. **Send Appearing Event**
   ```csharp
   await NavigationHelper.SendAppearingAsync(ShellProxy, component, Configuration);
   ```

---

## 2. Navigation Flow

### Overview
Navigation flows through the NaluShell system, coordinating between MAUI Shell and the Reactor components.

### 2.1 Navigation Initiation

**Entry Point:** `INavigationService.GoToAsync(INavigationInfo navigation)`

#### Flow Steps:

1. **Validation**
   ```csharp
   if (navigation.Count == 0)
       throw new InvalidNavigationException("Navigation must contain at least one segment.");
   ```

2. **Semaphore Lock**
   ```csharp
   await _semaphore.WaitAsync().ConfigureAwait(true);
   ```
   - Ensures only one navigation operation at a time

3. **State Verification**
   ```csharp
   var currentLocation = shellProxy.Location;
   if (initialLocation != currentLocation)
   {
       // State has changed, abort navigation
       _semaphore.Release();
       return false;
   }
   ```

4. **Navigation Mode Detection**
   ```csharp
   var result = await (navigation switch
   {
       { IsAbsolute: true } => ExecuteAbsoluteNavigationAsync(...),
       _ => ExecuteRelativeNavigationAsync(...)
   });
   ```

---

### 2.2 Relative Navigation Flow

**Trigger:** Pop operations, push operations within current navigation context

**Location:** `Internals/NavigationService.ExecuteRelativeNavigationAsync()`

#### Flow Steps:

1. **Get Current Navigation Stack**
   ```csharp
   section ??= shellProxy.CurrentItem.CurrentSection;
   stack ??= section.GetNavigationStack(ServiceProvider).ToList();
   ```

2. **Process Each Segment**
   Iterate through navigation segments (pop or push)

3. **POP Operation**
   
   a. **Check for Leaving Guard**
      ```csharp
      if (stackPage.PageComponent is ILeavingGuard)
      {
          await NavigationHelper.SendAppearingAsync(...);
          var canLeave = await NavigationHelper.CanLeaveAsync(...);
          if (!canLeave) return false;
      }
      ```
   
   b. **Send Disappearing Event**
      ```csharp
      await NavigationHelper.SendDisappearingAsync(ShellProxy, stackPage.PageComponent);
      ```
   
   c. **Send Leaving Event**
      ```csharp
      await NavigationHelper.SendLeavingAsync(ShellProxy, stackPage.PageComponent);
      ```
   
   d. **Pop from Navigation Stack**
      ```csharp
      stack.RemoveAt(stack.Count - 1);
      await shellProxy.PopAsync(section);
      ```
   
   e. **Add to Dispose Bag**
      ```csharp
      disposeBag.Add(stackPage.PageComponent);
      ```

4. **PUSH Operation**
   
   a. **Send Disappearing to Current Page**
      ```csharp
      await NavigationHelper.SendDisappearingAsync(ShellProxy, stackPage.PageComponent);
      ```
   
   b. **Create New Component** (see Section 1.2)
   c. **Send Entering Event**
      ```csharp
      await NavigationHelper.SendEnteringAsync(ShellProxy, component, Configuration);
      ```
   
   d. **Push to Stack** (see Section 1.2)
   e. **Add to Internal Tracking**

5. **Send Appearing to Target**
   ```csharp
   await NavigationHelper.SendAppearingAsync(ShellProxy, page.PageComponent, Configuration);
   ```

---

### 2.3 Absolute Navigation Flow

**Trigger:** Navigation to a different ShellContent, ShellSection, or ShellItem

**Location:** `Internals/NavigationService.ExecuteAbsoluteNavigationAsync()`

#### Flow Steps:

1. **Determine Navigation Type**
   - Same ShellContent → Relative navigation
   - Different ShellContent → Cross-content navigation
   - Different ShellSection → Cross-section navigation
   - Different ShellItem → Cross-item navigation

2. **Handle Modals First**
   ```csharp
   var modalPages = navigationStack.Count(page => page.IsModal);
   if (modalPages > 0)
   {
       var popModalsNavigation = PopTimes(modalPages);
       await ExecuteRelativeNavigationAsync(popModalsNavigation, ...);
   }
   ```

3. **Determine Leave Mode**
   ```csharp
   if (targetItem != currentItem)
   {
       if (behavior.HasFlag(NavigationBehavior.PopAllPagesOnItemChange))
           leaveMode = ContentLeaveMode.Destroy;
       else
           leaveMode = ContentLeaveMode.None;
   }
   // Similar logic for section changes
   ```

4. **Execute Cross-Content Navigation**
   **Location:** `Internals/NavigationService.ExecuteCrossContentNavigationAsync()`
   
   a. **Get Contents to Leave**
      - All ShellContents in current item (for cross-item)
      - All ShellContents in current section (for cross-section)
      - Only current ShellContent (for same content)
   
   b. **For Each Content to Leave:**
   
      **i. Get Navigation Stack**
      ```csharp
      var navigationStack = contentToLeave.Parent
          .GetNavigationStack(ServiceProvider, contentToLeave).ToList();
      ```
      
      **ii. Pop Stack Pages** (if leaveMode == ClearStack or Destroy)
      ```csharp
      var popCount = navigationStack.Count - 1;
      var popNavigation = PopTimes(popCount);
      await ExecuteRelativeNavigationAsync(popNavigation, ...);
      ```
      
      **iii. Check Leaving Guard** (if root component is ILeavingGuard)
      ```csharp
      if (!ignoreGuards && component is ILeavingGuard)
      {
          var canLeave = await NavigationHelper.CanLeaveAsync(...);
          if (!canLeave) return false;
      }
      ```
      
      **iv. Send Disappearing and Leaving**
      ```csharp
      await NavigationHelper.SendDisappearingAsync(ShellProxy, component);
      await NavigationHelper.SendLeavingAsync(ShellProxy, component);
      ```
      
      **v. Add to Dispose Bag** (if destroy mode)
      ```csharp
      if (leaveMode == ContentLeaveMode.Destroy)
      {
          disposeBag.Add(contentToLeave);
      }
      ```
   
   c. **Dispose ShellSections** (if destroy mode and different section)
      ```csharp
      if (sectionToLeave != targetContent.Parent)
      {
          disposeBag.Add(sectionToLeave);
      }
      ```
   
   d. **Navigate to Target**
   
      **i. Get or Create Target Content**
      ```csharp
      var (targetComponent, targetContentPage) = targetContent.GetOrCreateContent();
      ```
      
      **ii. Send Entering Event**
      ```csharp
      await NavigationHelper.SendEnteringAsync(ShellProxy, targetComponent, Configuration);
      ```
      
      **iii. Select ShellContent**
      ```csharp
      await ShellProxy.SelectContentAsync(targetContent.SegmentName);
      ```
      
      **iv. Navigate Remaining Segments** (relative navigation)
      ```csharp
      var relativeNavigation = ToRelativeNavigation(navigation, targetStack);
      await ExecuteRelativeNavigationAsync(relativeNavigation, ...);
      ```
      
      **v. Send Appearing Event**
      ```csharp
      await NavigationHelper.SendAppearingAsync(ShellProxy, targetComponent, Configuration);
      ```

5. **Commit Navigation**
   ```csharp
   await shellProxy.CommitNavigationAsync(() =>
   {
       foreach (var toDispose in disposeBag)
       {
           DisposeElement(toDispose);
       }
   });
   ```

---

### 2.4 Shell Proxy Navigation Coordination

**Location:** `ShellInfo/ShellProxy`

#### Key Methods:

1. **BeginNavigation()**
   ```csharp
   public bool BeginNavigation()
   {
       _navigationTarget = _shell.CurrentState.Location.OriginalString;
       _navigationCurrentSection = CurrentItem.CurrentSection;
       return true;
   }
   ```
   - Marks start of navigation batch
   - Captures initial state

2. **PushAsync()**
   - Updates `_navigationTarget` with new route
   - Registers route factory
   - MAUI Shell reacts to target change

3. **PopAsync()**
   - Removes segment from `_navigationTarget`
   - Optionally calls `section.RemoveStackPages()` for different sections

4. **CommitNavigationAsync()**
   ```csharp
   public async Task CommitNavigationAsync(Action? completeAction = default)
   {
       // Build target URI for Shell
       var targetState = BuildTargetState(_navigationTarget);
       
       // Navigate via Shell
       await _shell.GoToAsync(targetState + "?nalu");
       
       // Wait for animation
       await Task.Delay(500).ConfigureAwait(true);
       
       // Execute disposal
       completeAction?.Invoke();
   }
   ```
   - Triggers actual Shell navigation
   - Waits for animation to complete
   - Executes disposal after navigation completes

5. **SelectContentAsync()**
   - Sets current ShellContent
   - Updates `_navigationTarget`
   - Triggers cross-content navigation

---

## 3. Page Disposal Flow

### Overview
Pages are disposed when they are removed from the navigation stack or when ShellContents/ShellSections are destroyed. The disposal process breaks the circular reference between Component and Page.

### 3.1 Disposal Trigger Points

#### A. Relative Navigation Pop
**Location:** `Internals/NavigationService.ExecuteRelativeNavigationAsync()`
```csharp
disposeBag.Add(stackPage.PageComponent);
```

#### B. Cross-Content Navigation (Destroy Mode)
**Location:** `Internals/NavigationService.ExecuteCrossContentNavigationAsync()`
```csharp
if (leaveMode == ContentLeaveMode.Destroy)
{
    disposeBag.Add(contentToLeave);
}
```

#### C. Shell Section Disposal
**Location:** `Internals/NavigationService.DisposeElement()` - IShellSectionProxy case
```csharp
case IShellSectionProxy sectionProxy:
{
    var removedPages = sectionProxy.RemoveStackPages();
    foreach (var page in removedPages)
    {
        var componentWeakRef = (WeakReference<MauiReactor.Component>)
            page.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
        
        if (componentWeakRef.TryGetTarget(out var component))
        {
            DisposeElement(component);
        }
    }
    break;
}
```

---

### 3.2 RemoveStackPages Flow

**Location:** `ShellInfo/ShellSectionProxy.RemoveStackPages()`

```csharp
public IEnumerable<Page> RemoveStackPages(int count = -1)
{
    var navigation = _section.Navigation;
    var removedPages = new List<Page>();
    
    if (count < 0)
    {
        count = navigation.NavigationStack.Count - 1;
    }
    
    while (count-- > 0)
    {
        var pageToRemove = navigation.NavigationStack[^1];
        MarkPageForRemoval(pageToRemove);  // Mark for Shell's internal handling
        navigation.RemovePage(pageToRemove);  // Remove from MAUI's navigation stack
        removedPages.Add(pageToRemove);
    }
    
    return removedPages;
}
```

**Key Points:**
- Marks pages for removal (helps Shell's internal navigation logic)
- Removes pages from MAUI's navigation stack
- Returns removed pages for disposal

---

### 3.3 DisposeElement Flow

**Location:** `Internals/NavigationService.DisposeElement()`

#### Case 1: MauiReactor.Component Disposal

```csharp
case MauiReactor.Component page:
{
    var contentPage = page.ContainerPage;
    if (contentPage is not default(Page))
    {
        // CRITICAL: Break circular reference by clearing ContainerPage
        ClearContainerPage(page);
        
        // Clear reference from Page to Component
        contentPage.SetValue(ReactorBindableProperties.PageComponentReferenceProperty, default);
        
        // Disconnect platform handlers
        DisconnectHandlerHelper.DisconnectHandlers(contentPage);
    }
    
    // Dispose navigation context (releases service scope)
    PageNavigationContext.Dispose(page);
    
    // Track for leak detection
    _leakDetector?.Track(contentPage);
    
    break;
}
```

**Disposal Steps:**

1. **Clear ContainerPage Reference** (Circular Reference Break)
   ```csharp
   [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_containerPage")]
   private extern static ref WeakReference<Page>? GetContainerPageField(...);
   
   private static void ClearContainerPage(MauiReactor.Component component) 
       => GetContainerPageField(component) = default;
   ```
   - Uses UnsafeAccessor to access private `_containerPage` field
   - Sets `WeakReference<Page>` to null
   - **This breaks the circular reference**

2. **Clear PageComponentReferenceProperty**
   ```csharp
   contentPage.SetValue(ReactorBindableProperties.PageComponentReferenceProperty, default);
   ```
   - Removes WeakReference from Page to Component
   - Second side of circular reference broken

3. **Disconnect Handlers**
   ```csharp
   DisconnectHandlerHelper.DisconnectHandlers(contentPage);
   ```
   - Disconnects platform-specific handlers
   - Releases native platform resources

4. **Dispose Navigation Context**
   ```csharp
   PageNavigationContext.Dispose(page);
   ```
   **Location:** `Internals/PageNavigationContext.Dispose()`
   
   ```csharp
   public void Dispose()
   {
       _serviceScope?.Dispose();  // Releases DI scope and all scoped services
       _serviceScope = default;
   }
   ```
   - Releases service scope created during page creation
   - Disposes all scoped DI services
   - Critical for preventing service leaks

5. **Track for Leak Detection**
   ```csharp
   _leakDetector?.Track(contentPage);
   ```
   - Tracks disposed pages for leak detection
   - Helps identify pages that weren't properly disposed

---

#### Case 2: IShellContentProxy Disposal

```csharp
case IShellContentProxy contentProxy:
{
    var contentPage = contentProxy.Page;
    if (contentPage is not default(Page))
    {
        // Disconnect handlers
        DisconnectHandlerHelper.DisconnectHandlers(contentPage);
        
        // Destroy content (clears ShellContent's content cache)
        contentProxy.DestroyContent();
        
        // Track for leak detection
        _leakDetector?.Track(contentPage);
    }
    break;
}
```

**DestroyContent Implementation:**
**Location:** `ShellInfo/ShellContentProxy.DestroyContent()`

```csharp
public void DestroyContent()
{
    // Clear reference on ShellContent
    _content?.SetValue(ReactorBindableProperties.PageComponentReferenceProperty, default);
    
    if (Page is not { } page)
    {
        return;
    }
    
    var pageComponentWeakRef = (WeakReference<MauiReactor.Component>)
        page.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
    
    if (pageComponentWeakRef.TryGetTarget(out var pageComponent))
    {
        // Dispose navigation context
        PageNavigationContext.Dispose(pageComponent);
        
        // Clear navigation context reference
        var navContextProp = (PageNavigationContext)page
            .GetValue(PageNavigationContext.NavigationContextProperty);
        navContextProp?.Dispose();
        page.SetValue(PageNavigationContext.NavigationContextProperty, default);
        
        // Clear component reference
        page.SetValue(ReactorBindableProperties.PageComponentReferenceProperty, default);
    }
    
    // Clear ShellContent's internal content cache
    _shellContentCacheProperty.SetValue(_content, default);
}
```

**Key Points:**
- Handles root content pages
- Clears ShellContent's content cache (prevents recreation)
- Disposes component and navigation context
- Clears all references

---

#### Case 3: IShellSectionProxy Disposal

```csharp
case IShellSectionProxy sectionProxy:
{
    // Remove all pages from navigation stack
    var removedPages = sectionProxy.RemoveStackPages();
    
    // Dispose each page's component
    foreach (var page in removedPages)
    {
        var componentWeakRef = (WeakReference<MauiReactor.Component>)
            page.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
        
        if (componentWeakRef.TryGetTarget(out var component))
        {
            DisposeElement(component);
        }
    }
    break;
}
```

**Key Points:**
- Used during cross-item/section navigation with Destroy mode
- Removes all stack pages
- Recursively disposes each page's component

---

### 3.4 Circular Reference Breakdown

**Before Disposal:**
```
MauiReactor.Component
    ↓ (_containerPage: WeakReference<Page>)
Native MAUI Page
    ↓ (PageComponentReferenceProperty: WeakReference<MauiReactor.Component>)
[Strong cycle maintained - GC cannot reclaim]
```

**After Disposal:**
```
MauiReactor.Component
    ↓ (_containerPage: null)
[cycle broken - GC can reclaim]

Native MAUI Page
    ↓ (PageComponentReferenceProperty: null)
[cycle broken - GC can reclaim]
```

**Critical Step:** `ClearContainerPage()` is the essential fix that breaks the circular reference by setting the `_containerPage` field to null.

---

### 3.5 Handler Disconnection Flow

**Location:** `Internals/DisconnectHandlerHelper.DisconnectHandlers()`

```csharp
public static void DisconnectHandlers(IView view)
{
    if (view is VisualElement { IsLoaded: true } && 
        _onUnloaded is not default(Func<IElement, Action, IDisposable>))
    {
        // Schedule disconnection for when element unloads
        _onUnloaded(view, () =>
        {
            SafeDisconnectHandlers(view);
        });
    }
    else
    {
        // Disconnect immediately
        SafeDisconnectHandlers(view);
    }
}

private static void SafeDisconnectHandlers(IView view)
{
    try
    {
        view.Handler?.DisconnectHandler();
    }
    catch (Exception ex)
    {
        // Log exception
        Application.Current?.GetLogger<IViewHandler>()
            ?.LogError(ex, "Error disconnecting handlers for view: {ViewType}", view.GetType().Name);
    }
}
```

**Key Points:**
- Safely disconnects platform handlers
- Schedules disconnection if element is loaded (prevents crashes)
- Catches and logs exceptions (defensive programming)

---

### 3.6 Complete Disposal Lifecycle

```
Navigation Triggered
    ↓
Pages Added to Dispose Bag
    ↓
Navigation Commits
    ↓
For Each Item in Dispose Bag:
    ↓
DisposeElement(toDispose)
    ↓
[Case MauiReactor.Component]
    ↓
ClearContainerPage(page)  ← CRITICAL: Breaks circular reference
    ↓
Clear PageComponentReferenceProperty
    ↓
DisconnectHandlers(page)
    ↓
PageNavigationContext.Dispose(page)  ← Releases service scope
    ↓
LeakDetector.Track(page)
    ↓
[Case IShellContentProxy]
    ↓
DisconnectHandlers(page)
    ↓
DestroyContent()  ← Clears ShellContent cache
    ↓
LeakDetector.Track(page)
    ↓
[Case IShellSectionProxy]
    ↓
RemoveStackPages()  ← Returns all pages
    ↓
For Each Page:
    Extract Component from PageComponentReferenceProperty
    ↓
    Recursively call DisposeElement(component)
    ↓
All References Cleared
    ↓
GC Can Reclaim Memory
```

---

## 4. Lifecycle Event Flow

### 4.1 Entering Event

**When:** Before navigation completes

**Implementation:** `Internals/NavigationHelper.SendEnteringAsync()`

```csharp
public static ValueTask SendEnteringAsync(IShellProxy shell, MauiReactor.Component pageComponent, INavigationConfiguration configuration)
{
    var context = PageNavigationContext.Get(pageComponent);
    
    if (context.Entered) return ValueTask.CompletedTask;
    
    context.Entered = true;
    
    if (pageComponent is IEnteringAware enteringAware)
    {
        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
            NavigationLifecycleEventType.Entering, pageComponent, NavigationLifecycleHandling.Handled));
        
        return enteringAware.OnEnteringAsync();
    }
    
    shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
        NavigationLifecycleEventType.Entering, pageComponent, NavigationLifecycleHandling.NotHandled));
    
    return ValueTask.CompletedTask;
}
```

**Key Points:**
- Called once per page instance
- Only if page implements `IEnteringAware`
- Can be async (must complete synchronously for root pages)

---

### 4.2 Leaving Event

**When:** Before page is removed from navigation stack

**Implementation:** `Internals/NavigationHelper.SendLeavingAsync()`

```csharp
public static ValueTask SendLeavingAsync(IShellProxy shell, MauiReactor.Component pageComponent)
{
    var context = PageNavigationContext.Get(pageComponent);
    
    if (!context.Entered) return ValueTask.CompletedTask;
    
    context.Entered = false;  // Reset for re-entry
    
    if (pageComponent is ILeavingAware leavingAware)
    {
        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
            NavigationLifecycleEventType.Leaving, pageComponent, NavigationLifecycleHandling.Handled));
        
        return leavingAware.OnLeavingAsync();
    }
    
    shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
        NavigationLifecycleEventType.Leaving, pageComponent, NavigationLifecycleHandling.NotHandled));
    
    return ValueTask.CompletedTask;
}
```

**Key Points:**
- Only if page implements `ILeavingAware`
- Resets `Entered` flag (allows re-entry)
- Called before disposal

---

### 4.3 Appearing Event

**When:** After navigation completes and page is visible

**Implementation:** `Internals/NavigationHelper.SendAppearingAsync()`

```csharp
public static ValueTask SendAppearingAsync(IShellProxy shell, MauiReactor.Component pageComponent, INavigationConfiguration configuration, Action<object> propsDelOnAppearing = default)
{
    var context = PageNavigationContext.Get(pageComponent);
    
    if (context.Appeared) return ValueTask.CompletedTask;
    
    context.Appeared = true;
    
    // Apply props if provided (for deep navigation)
    if (propsDelOnAppearing is not default(Action<object>))
    {
        if (pageComponent.GetType().GetProperty("Props", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo propsProp)
        {
            propsDelOnAppearing.Invoke(propsProp.GetValue(pageComponent));
        }
    }
    
    if (pageComponent is IAppearingAware appearingAware)
    {
        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
            NavigationLifecycleEventType.Appearing, pageComponent, NavigationLifecycleHandling.Handled));
        
        return appearingAware.OnAppearingAsync();
    }
    
    shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
        NavigationLifecycleEventType.Appearing, pageComponent, NavigationLifecycleHandling.NotHandled));
    
    return ValueTask.CompletedTask;
}
```

**Key Points:**
- Only if page implements `IAppearingAware`
- Can apply props delegate (for deep navigation scenarios)
- Called after navigation animation completes

---

### 4.4 Disappearing Event

**When:** Before page is hidden (pop or navigate away)

**Implementation:** `Internals/NavigationHelper.SendDisappearingAsync()`

```csharp
public static ValueTask SendDisappearingAsync(IShellProxy shell, MauiReactor.Component pageComponent)
{
    var context = PageNavigationContext.Get(pageComponent);
    
    if (!context.Appeared) return ValueTask.CompletedTask;
    
    context.Appeared = false;  // Reset for re-appearance
    
    if (pageComponent is IDisappearingAware disappearingAware)
    {
        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
            NavigationLifecycleEventType.Disappearing, pageComponent, NavigationLifecycleHandling.Handled));
        
        return disappearingAware.OnDisappearingAsync();
    }
    
    shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
        NavigationLifecycleEventType.Disappearing, pageComponent, NavigationLifecycleHandling.NotHandled));
    
    return ValueTask.CompletedTask;
}
```

**Key Points:**
- Only if page implements `IDisappearingAware`
- Resets `Appeared` flag (allows re-appearance)
- Called before leaving and disposal

---

### 4.5 Leaving Guard

**When:** Before page is removed (navigation can be cancelled)

**Implementation:** `Internals/NavigationHelper.CanLeaveAsync()`

```csharp
public static ValueTask<bool> CanLeaveAsync(IShellProxy shell, MauiReactor.Component pageComponent)
{
    if (pageComponent is ILeavingGuard leavingGuard)
    {
        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(
            NavigationLifecycleEventType.LeavingGuard, pageComponent));
        
        return leavingGuard.CanLeaveAsync();
    }
    
    return ValueTask.FromResult(true);
}
```

**Key Points:**
- Only if page implements `ILeavingGuard`
- Can cancel navigation by returning `false`
- Called after appearing (page is visible when guard is checked)

---

### 4.6 Lifecycle Event Order

**For Push Navigation:**
```
Current Page: Disappearing
    ↓
Current Page: Leaving
    ↓
New Page: Entering
    ↓
Shell Navigation (Animation)
    ↓
New Page: Appearing
```

**For Pop Navigation:**
```
Current Page: Disappearing
    ↓
Current Page: Appearing (leaving guard)
    ↓
Current Page: Leaving (if guard allows)
    ↓
Shell Navigation (Animation)
    ↓
Previous Page: Appearing
```

**For Cross-Content Navigation:**
```
Current Root Page: Disappearing
    ↓
Stack Pages: Disappearing + Leaving (for each, if destroying)
    ↓
Current Root Page: Leaving (if guard allows)
    ↓
Target Root Page: Entering
    ↓
Shell Navigation (Animation)
    ↓
Target Root Page: Appearing
    ↓
Additional Stack Pages: Entering + Appearing (for each, if any)
```

---

## 5. Navigation Stack Tracking

### 5.1 NavigationStackPage Structure

**Location:** `Internals/NavigationStackPage.cs`

```csharp
public class NavigationStackPage
{
    public string Route { get; }
    public string SegmentName { get; }
    public MauiReactor.Component PageComponent { get; }
    public bool IsModal { get; }
}
```

**Purpose:** Internal tracking of navigation stack pages

---

### 5.2 GetNavigationStack Implementation

**Location:** `ShellInfo/ShellSectionProxy.GetNavigationStack()`

```csharp
public IEnumerable<NavigationStackPage> GetNavigationStack(IServiceProvider serviceProvider, IShellContentProxy? content = default)
{
    content ??= CurrentContent;
    
    if (content.Page is default(Page))
    {
        yield break;
    }
    
    // Root content page
    var baseRoute = $"//{Parent.SegmentName}/{SegmentName}/{content.SegmentName}";
    
    var componentWeakRef = (WeakReference<MauiReactor.Component>)
        content.Content.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
    
    if (componentWeakRef.TryGetTarget(out var component))
        yield return new NavigationStackPage(baseRoute, content.SegmentName, component, false);
    
    if (content != CurrentContent)
    {
        yield break;
    }
    
    var navigation = _section.Navigation;
    var route = new StringBuilder(baseRoute);
    
    // Navigation stack pages
    foreach (var stackPage in navigation.NavigationStack)
    {
        if (stackPage is not default(Page))
        {
            var segmentName = NavigationSegmentAttribute.GetSegmentName(stackPage.GetType());
            route.Append('/');
            route.Append(segmentName);
            
            var pageComponentValueWeakRef = (WeakReference<MauiReactor.Component>)
                stackPage.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
            
            if (pageComponentValueWeakRef.TryGetTarget(out var pageComponentValue))
            {
                yield return new NavigationStackPage(route.ToString(), segmentName, pageComponentValue, false);
            }
        }
    }
    
    // Modal stack pages
    foreach (var stackPage in navigation.ModalStack)
    {
        if (stackPage is not default(Page))
        {
            var segmentName = NavigationSegmentAttribute.GetSegmentName(stackPage.GetType());
            route.Append('/');
            route.Append(segmentName);
            
            var pageComponentValueWeakRef = (WeakReference<MauiReactor.Component>)
                stackPage.GetValue(ReactorBindableProperties.PageComponentReferenceProperty);
            
            if (pageComponentValueWeakRef.TryGetTarget(out var pageComponentValue))
            {
                yield return new NavigationStackPage(route.ToString(), segmentName, pageComponentValue, true);
            }
        }
    }
}
```

**Key Points:**
- Returns all pages in navigation stack (root + pushed + modals)
- Uses WeakReference to get components from pages
- Builds route string for each page
- Marks modal pages separately

---

## 6. Summary

### Key Flows:

1. **Page Creation:**
   - Root pages: Created via ShellContentProxy.GetOrCreateContent()
   - Stack pages: Created via NavigationService.CreatePage()
   - Circular reference established (Component ↔ Page)

2. **Navigation:**
   - Initiated via INavigationService.GoToAsync()
   - Coordinates with MAUI Shell via ShellProxy
   - Handles relative and absolute navigation
   - Manages lifecycle events
   - Tracks pages in dispose bag

3. **Page Disposal:**
   - Triggered on pop, cross-content navigation, or section destruction
   - Clears both sides of circular reference (ContainerPage + PageComponentReferenceProperty)
   - Disconnects handlers
   - Disposes navigation context (service scope)
   - Tracks for leak detection

### Critical Points:

- **Circular Reference:** Component has `_containerPage: WeakReference<Page>`, Page has `PageComponentReferenceProperty: WeakReference<MauiReactor.Component>`
- **Disposal Fix:** `ClearContainerPage()` sets `_containerPage` field to null, breaking the circular reference
- **Service Scope:** Each page gets its own DI scope, disposed on page disposal
- **Lifecycle Events:** Entering → Appearing (visible) → Disappearing → Leaving (removed)
- **Navigation Safety:** Semaphore ensures only one navigation at a time
- **Handler Disconnection:** Safely disconnects platform handlers during disposal

### Memory Management:

The disposal flow is critical for preventing memory leaks. Without proper disposal:
- Components remain alive (via ContainerPage reference)
- Pages remain alive (via WeakReference target)
- Service scopes are not released
- Handlers remain connected
- **Result: Memory leak**

With proper disposal:
- Circular reference is broken
- All references are cleared
- Handlers are disconnected
- Service scopes are released
- **Result: GC can reclaim memory**