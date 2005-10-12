<?xml version="1.0" encoding="windows-1250" ?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:param name="external-base">../build/net-1.1-debug/web/</xsl:param>

    <xsl:template name="external-iframe">
        <table cellpadding="0" cellspacing="0" style="width: 98%; table-layout: fixed" border="0" class="embeddedsource">
            <tr>
                <td>
                    <div class="sourcecode">
                        <xsl:copy-of select="document(concat($external-base,'/',@src,'.xhtml'))" />
                    </div>
                </td>
            </tr>
            <tr class="downloadlink">
                <td valign="top" style="font-size: 12px"><a href="{@src}">Download this file</a></td>
            </tr>
        </table>
    </xsl:template>

    <xsl:template match="cs[@src]">
        <xsl:call-template name="external-iframe" />
    </xsl:template>

    <xsl:template match="js[@src]">
        <xsl:call-template name="external-iframe" />
    </xsl:template>

    <xsl:template match="xml[@src]">
        <xsl:call-template name="external-iframe" />
    </xsl:template>

    <xsl:template match="x">
        <xsl:apply-templates mode="xml-example" />
    </xsl:template>

    <xsl:template match="link">
        <a href="{@href}.{$file_extension}"><xsl:apply-templates /></a>
    </xsl:template>

    <xsl:template match="*" mode="xml-example">
        <xsl:choose>
            <xsl:when test="count(descendant::node()) = 0">
                <span class="xmlbracket">&lt;</span>
                <span class="xmlelement"><xsl:value-of select="name()" /></span>
                <xsl:apply-templates select="@*" mode="xml-example" />
                <span class="xmlbracket"> /&gt;</span>
            </xsl:when>
            <xsl:otherwise>
                <span class="xmlbracket">&lt;</span>
                <span class="xmlelement"><xsl:value-of select="name()" /></span>
                <xsl:apply-templates select="@*" mode="xml-example" />
                <span class="xmlbracket">&gt;</span>
                <xsl:apply-templates mode="xml-example" />
                <span class="xmlbracket">&lt;/</span>
                <span class="xmlelement"><xsl:value-of select="name()" /></span>
                <span class="xmlbracket">&gt;</span>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="@*[name()='xml:space']" mode="xml-example"></xsl:template>
    <xsl:template match="@*" mode="xml-example">
        <span class="xmlattribute"><xsl:text> </xsl:text><xsl:value-of select="name()"/></span>
        <span class="xmlpunct">=</span><span class="xmlattribtext">"<xsl:value-of select="." />"</span>
    </xsl:template>

    <xsl:template match="comment()" mode="xml-example">
        <span class="xmlcomment">&lt;!--<xsl:value-of select="." />--&gt;</span>
    </xsl:template>
    <xsl:template match="node()" mode="xml-example" priority="-10">
        <xsl:copy>
            <xsl:apply-templates mode="xml-example" />
        </xsl:copy>
    </xsl:template>

    <xsl:template match="xml[@src]" mode="slashdoc">
        <xsl:call-template name="external-iframe" />
    </xsl:template>
    
    <xsl:template match="js[@src]" mode="slashdoc">
        <xsl:call-template name="external-iframe" />
    </xsl:template>
    
    <xsl:template match="cs[@src]" mode="slashdoc">
        <xsl:call-template name="external-iframe" />
    </xsl:template>
</xsl:stylesheet>
