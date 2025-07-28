# DeskDefender Phase 1 UAT Feedback Plan

## Overview
This plan addresses user feedback from initial testing of the event batching and summarization feature. Items will be implemented sequentially to improve user experience, data persistence, and application usability.

## Priority Order & Task List

### 🎨 **UI/UX Improvements (High Priority)**
- [ ] **1. Fix Menu Readability** - Change menu font colors for better contrast against background
- [ ] **2. Implement Dark Theme** - Set dark theme as default with light theme option in settings
- [ ] **3. Add Event Severity Legend** - Create legend showing color meanings (red=critical, yellow=medium, green=low, gray=info)
- [ ] **4. Log Detail Modal** - Add double-click functionality to show full log text in popup modal

### ⚙️ **Settings & Configuration (Medium Priority)**  
- [ ] **5. Create Settings Menu** - Move interval configuration to top menu settings panel
- [ ] **6. Settings Persistence** - Save and restore user settings between sessions
- [ ] **7. Theme Selection** - Add theme toggle in settings menu (dark/light)

### 💾 **Data Persistence & Management (Medium Priority)**
- [ ] **8. Event Batch Storage** - Verify database stores summaries not individual events
- [ ] **9. Session Persistence** - Ensure event logs persist between app sessions
- [ ] **10. Log Export** - Add menu option to export logs to file (CSV/JSON)

### 🧹 **Data Lifecycle Management (Lower Priority)**
- [ ] **11. Auto-Purge by Age** - Remove logs older than 30 days on app startup
- [ ] **12. Database Size Management** - Warn user when database approaches size threshold and offer purge options

## Implementation Notes

### Technical Considerations:
- **Settings Storage**: Use JSON config file or registry for user preferences
- **Database Schema**: Ensure EventSummary objects are stored, not raw input events
- **Theme System**: Implement ResourceDictionary-based theming for WPF
- **Export Format**: Support both CSV (Excel-friendly) and JSON (structured data)
- **Purge Logic**: Configurable thresholds with user confirmation for size-based purging

### UI/UX Guidelines:
- **Dark Theme Colors**: Use standard dark theme palette (#2C3E50, #34495E, etc.)
- **Severity Colors**: Red (#E74C3C), Yellow (#F39C12), Green (#27AE60), Gray (#95A5A6)
- **Modal Design**: Clean, centered popup with scrollable text area
- **Settings Layout**: Organized tabs/sections for different setting categories

## Success Criteria
- [ ] All menu items clearly readable in both themes
- [ ] User preferences persist across app restarts
- [ ] Full log details accessible via double-click
- [ ] Settings centralized in intuitive menu location
- [ ] Color legend helps users understand event severity
- [ ] Theme switching works seamlessly
- [ ] Database efficiently stores batch summaries
- [ ] Event history preserved between sessions
- [ ] Log export functionality works reliably
- [ ] Automatic cleanup prevents database bloat

## Estimated Timeline
- **Phase 1A (UI/UX)**: 2-3 implementation sessions
- **Phase 1B (Settings)**: 1-2 implementation sessions  
- **Phase 1C (Persistence)**: 2-3 implementation sessions
- **Phase 1D (Lifecycle)**: 1-2 implementation sessions

**Total Estimated Time**: 6-10 implementation sessions
