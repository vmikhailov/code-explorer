import React, { useState, useRef, useCallback, useEffect } from 'react';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  XIcon,
  DockRightIcon,
  FloatIcon,
} from './Icons';

export type DockCorner = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
export type CollapseAxis = 'horizontal' | 'vertical';

const CornerGripIcon: React.FC = () => (
  <svg
    width="10"
    height="10"
    viewBox="0 0 10 10"
    fill="none"
    className="floating-dock-corner-grip"
    aria-hidden="true"
  >
    <path
      d="M8.5 1.5L1.5 8.5M8.5 4.5L4.5 8.5M8.5 7.5L7.5 8.5"
      stroke="currentColor"
      strokeWidth="1.2"
      strokeLinecap="round"
      opacity="0.6"
    />
  </svg>
);

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

  /**
   * Optional height of expanded panel (e.g. 400, '350px').
   */
  height?: number | string;

  minWidth?: number;
  maxWidth?: number | string;
  minHeight?: number;
  maxHeight?: number | string;

  /**
   * Enables interactive resizing by dragging borders and corners (default true).
   */
  resizable?: boolean;

  /**
   * Storage key to persist user's resized dimensions in localStorage (e.g. 'ce_orbit_legend').
   */
  persistKey?: string;

  /**
   * Callback fired during resizing.
   */
  onResize?: (width: number, height: number) => void;

  /**
   * When true, renders panel in docked layout mode (fits 100% of parent resizable panel container)
   * without absolute positioning and without custom floating resize handles.
   */
  docked?: boolean;

  /**
   * Optional toggle callback to switch between docked and floating mode.
   */
  onToggleDock?: () => void;
  dockTooltip?: string;

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
  height,
  minWidth = 260,
  maxWidth,
  minHeight = 120,
  maxHeight,
  resizable = true,
  persistKey,
  onResize,
  docked = false,
  onToggleDock,
  dockTooltip,
  ariaLabel,
  className = '',
  headerClassName = '',
  bodyClassName = '',
  style,
  children,
}) => {
  const isRight = dock === 'top-right' || dock === 'bottom-right';
  const isBottom = dock === 'bottom-left' || dock === 'bottom-right';

  const parseDimension = (val?: number | string): number | null => {
    if (typeof val === 'number') return val;
    if (typeof val === 'string') {
      const p = parseFloat(val);
      if (!isNaN(p)) return p;
    }
    return null;
  };

  const initialWidth = parseDimension(width) ?? (isRight ? 360 : 340);
  const initialHeight = parseDimension(height);

  const [panelWidth, setPanelWidth] = useState<number>(() => {
    if (typeof window !== 'undefined' && window.localStorage && persistKey) {
      try {
        const saved = localStorage.getItem(`${persistKey}_w`);
        if (saved) {
          const parsed = parseFloat(saved);
          if (!isNaN(parsed) && parsed >= minWidth) return parsed;
        }
      } catch {}
    }
    return initialWidth;
  });

  const [panelHeight, setPanelHeight] = useState<number | undefined>(() => {
    if (typeof window !== 'undefined' && window.localStorage && persistKey) {
      try {
        const saved = localStorage.getItem(`${persistKey}_h`);
        if (saved) {
          const parsed = parseFloat(saved);
          if (!isNaN(parsed) && parsed >= minHeight) return parsed;
        }
      } catch {}
    }
    return initialHeight ?? undefined;
  });

  const [isResizing, setIsResizing] = useState(false);
  const panelRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const parsed = parseDimension(width);
    if (parsed !== null && !persistKey) {
      setPanelWidth(parsed);
    }
  }, [width, persistKey]);

  useEffect(() => {
    const parsed = parseDimension(height);
    if (parsed !== null && !persistKey) {
      setPanelHeight(parsed);
    }
  }, [height, persistKey]);

  const startResize = useCallback(
    (e: React.PointerEvent, handleDir: 'x' | 'y' | 'xy') => {
      e.preventDefault();
      e.stopPropagation();

      const startX = e.clientX;
      const startY = e.clientY;
      const startW = panelRef.current ? panelRef.current.getBoundingClientRect().width : panelWidth;
      const startH = panelRef.current ? panelRef.current.getBoundingClientRect().height : (panelHeight || 300);

      setIsResizing(true);
      if (typeof document !== 'undefined') {
        document.body.style.userSelect = 'none';
      }

      const resolvedMinWidth = minWidth;
      const resolvedMaxWidth =
        typeof maxWidth === 'number'
          ? maxWidth
          : typeof window !== 'undefined'
          ? window.innerWidth - 32
          : 900;
      const resolvedMinHeight = minHeight;
      const resolvedMaxHeight =
        typeof maxHeight === 'number'
          ? maxHeight
          : typeof window !== 'undefined'
          ? window.innerHeight - 32
          : 900;

      let currentW = startW;
      let currentH = startH;

      const handlePointerMove = (moveEvt: PointerEvent) => {
        const deltaX = moveEvt.clientX - startX;
        const deltaY = moveEvt.clientY - startY;

        if (handleDir === 'x' || handleDir === 'xy') {
          const newW = isRight ? startW - deltaX : startW + deltaX;
          const clampedW = Math.max(resolvedMinWidth, Math.min(resolvedMaxWidth, Math.round(newW)));
          currentW = clampedW;
          setPanelWidth(clampedW);
        }

        if (handleDir === 'y' || handleDir === 'xy') {
          const newH = isBottom ? startH - deltaY : startH + deltaY;
          const clampedH = Math.max(resolvedMinHeight, Math.min(resolvedMaxHeight, Math.round(newH)));
          currentH = clampedH;
          setPanelHeight(clampedH);
        }

        onResize?.(currentW, currentH);
      };

      const handlePointerUp = () => {
        if (typeof window !== 'undefined') {
          window.removeEventListener('pointermove', handlePointerMove);
          window.removeEventListener('pointerup', handlePointerUp);
        }
        if (typeof document !== 'undefined') {
          document.body.style.userSelect = '';
        }
        setIsResizing(false);

        if (typeof window !== 'undefined' && window.localStorage && persistKey) {
          try {
            localStorage.setItem(`${persistKey}_w`, String(currentW));
            if (currentH) localStorage.setItem(`${persistKey}_h`, String(currentH));
          } catch {}
        }
      };

      if (typeof window !== 'undefined') {
        window.addEventListener('pointermove', handlePointerMove);
        window.addEventListener('pointerup', handlePointerUp);
      }
    },
    [isRight, isBottom, panelWidth, panelHeight, minWidth, maxWidth, minHeight, maxHeight, onResize, persistKey]
  );

  const handleResetSize = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      e.stopPropagation();
      const defW = parseDimension(width) ?? (isRight ? 360 : 340);
      const defH = parseDimension(height);
      setPanelWidth(defW);
      setPanelHeight(defH ?? undefined);
      if (typeof window !== 'undefined' && window.localStorage && persistKey) {
        try {
          localStorage.removeItem(`${persistKey}_w`);
          localStorage.removeItem(`${persistKey}_h`);
        } catch {}
      }
    },
    [width, height, isRight, persistKey]
  );

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

  if (docked) {
    containerStyle.position = 'relative';
    containerStyle.width = '100%';
    containerStyle.height = '100%';
    containerStyle.top = 'auto';
    containerStyle.left = 'auto';
    containerStyle.right = 'auto';
    containerStyle.bottom = 'auto';
    containerStyle.borderRadius = 0;
    containerStyle.boxShadow = 'none';
    containerStyle.borderTop = 'none';
    containerStyle.borderBottom = 'none';
  } else if (!isCollapsed) {
    containerStyle.width = `${panelWidth}px`;
    if (panelHeight !== undefined) {
      containerStyle.height = `${panelHeight}px`;
    }
    if (maxWidth !== undefined) containerStyle.maxWidth = typeof maxWidth === 'number' ? `${maxWidth}px` : maxWidth;
    if (maxHeight !== undefined) containerStyle.maxHeight = typeof maxHeight === 'number' ? `${maxHeight}px` : maxHeight;
  }

  const effectiveCollapsedIcon = collapsedIcon !== undefined ? collapsedIcon : icon;
  const effectiveCollapsedTitle = collapsedTitle !== undefined ? collapsedTitle : title;
  const effectiveCollapsedBadge = collapsedBadge !== undefined ? collapsedBadge : badge;

  return (
    <aside
      ref={panelRef as any}
      className={`floating-dock-panel dock-${dock} ${docked ? 'is-docked' : ''} ${isCollapsed ? 'is-collapsed' : ''} ${isResizing ? 'is-resizing' : ''} ${className}`.trim()}
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

                {onToggleDock && (
                  <button
                    type="button"
                    className="floating-dock-btn dock-toggle-btn"
                    onClick={onToggleDock}
                    title={dockTooltip || (docked ? 'Float window over canvas' : 'Dock to sidebar')}
                    aria-label={dockTooltip || (docked ? 'Float window over canvas' : 'Dock to sidebar')}
                  >
                    {docked ? <FloatIcon size={12} /> : <DockRightIcon size={12} />}
                  </button>
                )}

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

          {/* Interactive resize handles when expanded (only in floating mode) */}
          {resizable && !docked && (
            <>
              {/* Vertical Edge Handle */}
              <div
                className={`floating-dock-resize-handle-x ${isRight ? 'handle-left' : 'handle-right'}`}
                onPointerDown={(e) => startResize(e, 'x')}
                onDoubleClick={handleResetSize}
                title="Drag to resize width (Double-click to reset)"
              />

              {/* Horizontal Edge Handle */}
              <div
                className={`floating-dock-resize-handle-y ${isBottom ? 'handle-top' : 'handle-bottom'}`}
                onPointerDown={(e) => startResize(e, 'y')}
                onDoubleClick={handleResetSize}
                title="Drag to resize height (Double-click to reset)"
              />

              {/* Corner Grip Handle */}
              <div
                className={`floating-dock-resize-handle-corner ${
                  isBottom
                    ? isRight
                      ? 'corner-top-left'
                      : 'corner-top-right'
                    : isRight
                    ? 'corner-bottom-left'
                    : 'corner-bottom-right'
                }`}
                onPointerDown={(e) => startResize(e, 'xy')}
                onDoubleClick={handleResetSize}
                title="Drag to resize panel (Double-click to reset)"
              >
                <CornerGripIcon />
              </div>
            </>
          )}
        </>
      )}
    </aside>
  );
};
