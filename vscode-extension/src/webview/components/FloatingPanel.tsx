import React from 'react';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  XIcon,
} from './Icons';

export type DockCorner = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
export type CollapseAxis = 'horizontal' | 'vertical';

export interface FloatingPanelProps {
  /**
   * Docking corner in the viewport/canvas.
   * Controls absolute positioning and collapse direction.
   */
  dock: DockCorner;

  /**
   * Primary title displayed in the header and collapsed pill.
   */
  title: React.ReactNode;

  /**
   * Optional subtitle or metadata row.
   * Rendered on its own line below the main header bar to prevent element overlap.
   */
  subtitle?: React.ReactNode;

  /**
   * Leading icon in the header (clean SVG).
   */
  icon?: React.ReactNode;

  /**
   * Optional badge chip next to the title (e.g. `:DB`, `:SERVICE`).
   */
  badge?: React.ReactNode;

  /**
   * Custom action buttons in the header (e.g. 'Restore All', 'Reset').
   */
  headerActions?: React.ReactNode;

  /**
   * Collapsed state flag.
   */
  isCollapsed?: boolean;

  /**
   * Toggle callback when user collapses or expands the panel.
   */
  onToggleCollapse?: (collapsed: boolean) => void;

  /**
   * Optional close/deselect callback. If provided, renders a clean close button.
   */
  onClose?: () => void;

  closeTooltip?: string;
  collapseTooltip?: string;
  expandTooltip?: string;

  /**
   * Optional custom title for the collapsed badge. Defaults to `title`.
   */
  collapsedTitle?: React.ReactNode;

  /**
   * Optional custom badge in collapsed state (e.g. count or status).
   */
  collapsedBadge?: React.ReactNode;

  /**
   * Optional custom icon in collapsed state. Defaults to `icon`.
   */
  collapsedIcon?: React.ReactNode;

  /**
   * Axis of collapse: 'horizontal' collapses toward side edge (left/right);
   * 'vertical' collapses toward vertical edge (up/down).
   */
  collapseAxis?: CollapseAxis;

  /**
   * Width of expanded panel (e.g. 360, '340px').
   */
  width?: number | string;

  maxWidth?: number | string;
  maxHeight?: number | string;

  ariaLabel?: string;
  className?: string;
  headerClassName?: string;
  bodyClassName?: string;
  style?: React.CSSProperties;
  children?: React.ReactNode;
}

export const FloatingPanel: React.FC<FloatingPanelProps> = ({
  dock,
  title,
  subtitle,
  icon,
  badge,
  headerActions,
  isCollapsed = false,
  onToggleCollapse,
  onClose,
  closeTooltip = 'Close',
  collapseTooltip,
  expandTooltip,
  collapsedTitle,
  collapsedBadge,
  collapsedIcon,
  collapseAxis,
  width,
  maxWidth,
  maxHeight,
  ariaLabel,
  className = '',
  headerClassName = '',
  bodyClassName = '',
  style,
  children,
}) => {
  const isRight = dock === 'top-right' || dock === 'bottom-right';
  const isBottom = dock === 'bottom-left' || dock === 'bottom-right';

  // Determine collapse axis if not explicitly set
  const axis = collapseAxis || (isBottom && dock === 'bottom-left' ? 'horizontal' : 'horizontal');

  // Directional collapse/expand icons:
  // - If right-docked: expanded collapses towards right (ChevronRight); collapsed expands towards left (ChevronLeft).
  // - If left-docked: expanded collapses towards left (ChevronLeft); collapsed expands towards right (ChevronRight).
  // - If vertical: expanded collapses down (ChevronDown); collapsed expands up (ChevronUp).
  const renderCollapseIcon = () => {
    if (axis === 'vertical') {
      return isBottom ? <ChevronDownIcon size={13} /> : <ChevronUpIcon size={13} />;
    }
    return isRight ? <ChevronRightIcon size={13} /> : <ChevronLeftIcon size={13} />;
  };

  const renderExpandIcon = () => {
    if (axis === 'vertical') {
      return isBottom ? <ChevronUpIcon size={13} /> : <ChevronDownIcon size={13} />;
    }
    return isRight ? <ChevronLeftIcon size={13} /> : <ChevronRightIcon size={13} />;
  };

  const resolvedCollapseTooltip = collapseTooltip || (isCollapsed ? 'Expand panel' : 'Collapse panel');
  const resolvedExpandTooltip = expandTooltip || 'Click to expand';

  const containerStyle: React.CSSProperties = {
    ...style,
  };

  if (!isCollapsed) {
    if (width !== undefined) containerStyle.width = typeof width === 'number' ? `${width}px` : width;
    if (maxWidth !== undefined) containerStyle.maxWidth = typeof maxWidth === 'number' ? `${maxWidth}px` : maxWidth;
    if (maxHeight !== undefined) containerStyle.maxHeight = typeof maxHeight === 'number' ? `${maxHeight}px` : maxHeight;
  }

  const effectiveCollapsedIcon = collapsedIcon !== undefined ? collapsedIcon : icon;
  const effectiveCollapsedTitle = collapsedTitle !== undefined ? collapsedTitle : title;
  const effectiveCollapsedBadge = collapsedBadge !== undefined ? collapsedBadge : badge;

  return (
    <aside
      className={`floating-dock-panel dock-${dock} ${isCollapsed ? 'is-collapsed' : ''} ${className}`.trim()}
      style={containerStyle}
      role="complementary"
      aria-label={ariaLabel || (typeof title === 'string' ? title : 'Floating Panel')}
    >
      {isCollapsed ? (
        /* Minimalist collapsed pill clinging to docked corner */
        <div
          className="floating-dock-collapsed-pill"
          onClick={() => onToggleCollapse?.(false)}
          title={resolvedExpandTooltip}
        >
          {/* Chevron positioned inward based on edge */}
          {!isRight && <span className="collapsed-chevron">{renderExpandIcon()}</span>}
          {effectiveCollapsedBadge && <span className="collapsed-badge">{effectiveCollapsedBadge}</span>}
          {effectiveCollapsedIcon && <span className="collapsed-icon">{effectiveCollapsedIcon}</span>}
          <span className="collapsed-label">{effectiveCollapsedTitle}</span>
          {isRight && <span className="collapsed-chevron">{renderExpandIcon()}</span>}

          {onClose && (
            <button
              type="button"
              className="collapsed-close-btn"
              onClick={(e) => {
                e.stopPropagation();
                onClose();
              }}
              title={closeTooltip}
              aria-label={closeTooltip}
            >
              <XIcon size={12} />
            </button>
          )}
        </div>
      ) : (
        /* Full expanded floating window */
        <>
          <div className={`floating-dock-header ${headerClassName}`.trim()}>
            {/* Primary header bar: Title + Badge + Actions + Controls */}
            <div className="floating-dock-header-main">
              <div className="floating-dock-header-lead">
                {badge && <span className="floating-dock-badge">{badge}</span>}
                {icon && <span className="floating-dock-header-icon">{icon}</span>}
                <div className="floating-dock-title-wrap">
                  <h4 className="floating-dock-title" title={typeof title === 'string' ? title : undefined}>
                    {title}
                  </h4>
                </div>
              </div>

              <div className="floating-dock-header-controls">
                {headerActions}

                {onToggleCollapse && (
                  <button
                    type="button"
                    className="floating-dock-btn collapse-btn"
                    onClick={() => onToggleCollapse(true)}
                    title={resolvedCollapseTooltip}
                    aria-label={resolvedCollapseTooltip}
                  >
                    {renderCollapseIcon()}
                  </button>
                )}

                {onClose && (
                  <button
                    type="button"
                    className="floating-dock-btn close-btn"
                    onClick={onClose}
                    title={closeTooltip}
                    aria-label={closeTooltip}
                  >
                    <XIcon size={12} />
                  </button>
                )}
              </div>
            </div>

            {/* Subtitle / Metadata row (isolated row to prevent collision with buttons) */}
            {subtitle && <div className="floating-dock-header-sub">{subtitle}</div>}
          </div>

          {/* Panel body */}
          <div className={`floating-dock-body ${bodyClassName}`.trim()}>{children}</div>
        </>
      )}
    </aside>
  );
};
