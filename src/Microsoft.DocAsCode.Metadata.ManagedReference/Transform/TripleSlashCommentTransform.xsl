<?xml version="1.0"?>
<xsl:stylesheet
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:input="urn:input-variables"
  exclude-result-prefixes="xsl input"
  version="1.1">

  <xsl:output method="xml" indent="yes" encoding="UTF-8" />

  <xsl:param name="input:language"/>

  <xsl:template match="para">
    <p>
      <xsl:apply-templates />
    </p>
  </xsl:template>

  <xsl:template match="b">
    <strong>
      <xsl:apply-templates />
    </strong>
  </xsl:template>

  <xsl:template match="i">
    <em>
      <xsl:apply-templates />
    </em>
  </xsl:template>

  <xsl:template match="languageKeyword">
    <strong>
      <xsl:apply-templates />
    </strong>
  </xsl:template>

  <xsl:template match="ui">
    <strong>
      <xsl:apply-templates />
    </strong>
  </xsl:template>

  <xsl:template match="c">
    <code>
      <xsl:apply-templates />
    </code>
  </xsl:template>

  <xsl:template match="code">
    <pre>
      <code>
        <xsl:if test="normalize-space(@language)">
          <xsl:attribute name="class">
            <xsl:value-of select="@language" />
          </xsl:attribute>
        </xsl:if>
        <xsl:apply-templates />
      </code>
    </pre>
  </xsl:template>

  <xsl:template match="value">
    <returns>
      <xsl:apply-templates />
    </returns>
  </xsl:template>

  <xsl:template match="see[@langword]">
    <xref>
      <xsl:attribute name="uid">
        <xsl:value-of select="concat('langword_', $input:language, '_', @langword)"/>
      </xsl:attribute>
      <xsl:attribute name="name">
        <xsl:value-of select="@langword"/>
      </xsl:attribute>
      <xsl:attribute name="href">
      </xsl:attribute>
    </xref>
  </xsl:template>

  <xsl:template match="paramref">
    <xsl:if test="normalize-space(@name)">
      <em>
        <xsl:value-of select="@name" />
      </em>
    </xsl:if>
  </xsl:template>

  <xsl:template match="typeparamref">
    <xsl:if test="normalize-space(@name)">
      <em>
        <xsl:value-of select="@name" />
      </em>
    </xsl:if>
  </xsl:template>

  <xsl:template match="list">
    <xsl:variable name="listtype">
      <xsl:value-of select="normalize-space(@type)"/>
    </xsl:variable>
    <xsl:choose>
      <xsl:when test="$listtype = 'table'">
        <table>
          <xsl:if test="listheader">
            <thead>
              <tr>
                <th>
                  <xsl:apply-templates select="listheader/term" />
                </th>
                <th>
                  <xsl:apply-templates select="listheader/description" />
                </th>
              </tr>
            </thead>
          </xsl:if>
          <tbody>
            <xsl:for-each select="item">
              <tr>
                <td>
                  <xsl:apply-templates select="term"/>
                </td>
                <td>
                  <xsl:apply-templates select="description"/>
                </td>
              </tr>
            </xsl:for-each>
          </tbody>
        </table>
      </xsl:when>
      <xsl:otherwise>
        <xsl:if test="listheader">
          <p>
            <strong>
              <xsl:if test="listheader/term">
                <xsl:value-of select="concat(string(listheader/term),'-')"/>
              </xsl:if>
              <xsl:value-of select="string(listheader/description)" />
            </strong>
          </p>
        </xsl:if>
        <xsl:choose>
          <xsl:when test="$listtype = 'bullet'">
            <ul>
              <xsl:for-each select="item">
                <li>
                  <xsl:apply-templates select="term" />
                  <xsl:apply-templates select="description" />
                </li>
              </xsl:for-each>
            </ul>
          </xsl:when>
          <xsl:when test="$listtype = 'number'">
            <ol>
              <xsl:for-each select="item">
                <li>
                  <xsl:apply-templates select="term" />
                  <xsl:apply-templates select="description" />
                </li>
              </xsl:for-each>
            </ol>
          </xsl:when>
        </xsl:choose>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template match="description">
    <xsl:apply-templates />
  </xsl:template>

  <xsl:template match="term">
    <xsl:apply-templates />
  </xsl:template>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>